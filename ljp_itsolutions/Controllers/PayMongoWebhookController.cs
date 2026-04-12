using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ljp_itsolutions.Data;
using ljp_itsolutions.Models;
using ljp_itsolutions.Services;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace ljp_itsolutions.Controllers
{
    [ApiController]
    [Route("api/paymongo")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public class PayMongoWebhookController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<PayMongoWebhookController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IReceiptService _receiptService;
        private readonly IServiceScopeFactory _scopeFactory;

        public PayMongoWebhookController(ApplicationDbContext db, ILogger<PayMongoWebhookController> logger, IConfiguration configuration, IReceiptService receiptService, IServiceScopeFactory scopeFactory)
        {
            _db = db;
            _logger = logger;
            _configuration = configuration;
            _receiptService = receiptService;
            _scopeFactory = scopeFactory;
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> HandleWebhook()
        {
            var signatureHeader = Request.Headers["paymongo-signature"].ToString();
            var webhookSecret = _configuration.GetSection("PayMongo:WebhookSecretKey").Value;

            // Read raw body to ensure signature match
            using var ms = new MemoryStream();
            await Request.Body.CopyToAsync(ms);
            var requestBodyBytes = ms.ToArray();
            var json = Encoding.UTF8.GetString(requestBodyBytes);

            // Verify Signature
            if (!string.IsNullOrEmpty(signatureHeader) && !string.IsNullOrEmpty(webhookSecret))
            {
                if (!VerifySignature(requestBodyBytes, signatureHeader, webhookSecret))
                {
                    _logger.LogWarning("PayMongo Webhook Signature Verification Failed!");
                    return BadRequest("Invalid Signature");
                }
            }

            try
            {
                using var payload = JsonDocument.Parse(json);
                var root = payload.RootElement;
                
                // Extract event type
                var eventType = root.GetProperty("data").GetProperty("attributes").GetProperty("type").GetString();
                
                if (eventType == "payment.paid" || eventType == "checkout_session.payment.paid")
                {
                    await ProcessPaidEvent(root);
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing PayMongo webhook payload.");
                return BadRequest();
            }
        }

        private async Task ProcessPaidEvent(JsonDocument root)
        {
            // JsonDocument from outer scope might be disposed, but we're calling this within scope.
            // Using RootElement because JsonDocument.Parse returns a disposable.
            // We'll pass the root element instead for clarity.
        }

        // Overload for RootElement
        private async Task ProcessPaidEvent(JsonElement root)
        {
            var dataObj = root.GetProperty("data").GetProperty("attributes").GetProperty("data");
            var attributes = dataObj.GetProperty("attributes");
            
            string? externalRef = TryGetExternalReference(attributes);

            if (!string.IsNullOrEmpty(externalRef) && Guid.TryParse(externalRef, out var orderId))
            {
                var order = await _db.Orders
                    .Include(o => o.Payments)
                    .FirstOrDefaultAsync(o => o.OrderID == orderId);

                if (order != null)
                {
                    if (!VerifyPaymentAmount(order, attributes))
                    {
                        return; // Logged in helper
                    }

                    order.PaymentStatus = "Paid"; 
                    
                    UpdatePaymentRecords(order, dataObj);
                    
                    await _db.SaveChangesAsync();
                    await LogAudit($"PayMongo: Order #{orderId.ToString().Substring(0, 8)} confirmed paid.");
                    _logger.LogInformation("Order {OrderId} marked as paid via webhook.", orderId);

                    TryTriggerReceiptEmail(order);
                }
            }
        }

        private string? TryGetExternalReference(JsonElement attributes)
        {
            if (attributes.TryGetProperty("external_reference", out var refProperty) && refProperty.ValueKind != JsonValueKind.Null)
            {
                return refProperty.GetString();
            }
            if (attributes.TryGetProperty("metadata", out var metaProp) && metaProp.TryGetProperty("external_reference", out var metaRefProp))
            {
                return metaRefProp.GetString();
            }
            return null;
        }

        private bool VerifyPaymentAmount(Order order, JsonElement attributes)
        {
            if (attributes.TryGetProperty("amount", out var amountProp))
            {
                long amountInCentavos = amountProp.GetInt64();
                decimal amountInPesos = amountInCentavos / 100m;

                if (Math.Abs(amountInPesos - order.FinalAmount) > 0.01m)
                {
                    _logger.LogWarning("PayMongo Webhook Amount Mismatch! Order: {OrderId}, Expected: {Expected}, Received: {Received}", 
                        order.OrderID, order.FinalAmount, amountInPesos);
                    
                    _ = LogAudit($"PayMongo Security Alert: Amount mismatch for Order #{order.OrderID.ToString().Substring(0, 8)}. Expected {order.FinalAmount}, Received {amountInPesos}");
                    return false;
                }
                return true;
            }
            _logger.LogWarning("PayMongo Webhook Missing Amount! Order: {OrderId}", order.OrderID);
            return false;
        }

        private void UpdatePaymentRecords(Order order, JsonElement dataObj)
        {
            var payment = order.Payments.FirstOrDefault(p => p.PaymentMethod.Contains("Paymongo") || p.PaymentMethod == "E-Wallet");
            string reference = dataObj.GetProperty("id").GetString() ?? "N/A";

            if (payment != null)
            {
                payment.PaymentStatus = "Completed";
                payment.ReferenceNumber = reference;
                payment.AmountPaid = order.FinalAmount;
            }
            else
            {
                _db.Payments.Add(new Payment
                {
                    OrderID = order.OrderID,
                    AmountPaid = order.FinalAmount,
                    PaymentMethod = "Paymongo E-Wallet",
                    PaymentStatus = "Completed",
                    PaymentDate = DateTime.UtcNow,
                    ReferenceNumber = reference
                });
            }
        }

        private void TryTriggerReceiptEmail(Order order)
        {
            if (order.CustomerID.HasValue)
            {
                _ = Task.Run(async () => {
                    try {
                        using var scope = _scopeFactory.CreateScope();
                        // This would call the receipt service
                    } catch (Exception ex) {
                        _logger.LogError(ex, "Background webhook receipt sending failed for order {OrderId}", order.OrderID);
                    }
                });
            }
        }

        private bool VerifySignature(byte[] payloadBytes, string signatureHeader, string secret)
        {
            try
            {
                var parts = signatureHeader.Split(',');
                var timestamp = parts.FirstOrDefault(p => p.StartsWith("t="))?.Substring(2);
                var liveSignature = parts.FirstOrDefault(p => p.StartsWith("li="))?.Substring(3);
                var testSignature = parts.FirstOrDefault(p => p.StartsWith("te="))?.Substring(3);

                var signatureToCompare = !string.IsNullOrEmpty(liveSignature) ? liveSignature : testSignature;

                if (string.IsNullOrEmpty(timestamp) || string.IsNullOrEmpty(signatureToCompare))
                    return false;

                var timestampBytes = Encoding.UTF8.GetBytes(timestamp);
                var dotBytes = Encoding.UTF8.GetBytes(".");
                
                var baseBytes = new byte[timestampBytes.Length + dotBytes.Length + payloadBytes.Length];
                Buffer.BlockCopy(timestampBytes, 0, baseBytes, 0, timestampBytes.Length);
                Buffer.BlockCopy(dotBytes, 0, baseBytes, timestampBytes.Length, dotBytes.Length);
                Buffer.BlockCopy(payloadBytes, 0, baseBytes, timestampBytes.Length + dotBytes.Length, payloadBytes.Length);

                var keyBytes = Encoding.UTF8.GetBytes(secret);

                using var hmac = new HMACSHA256(keyBytes);
                var hashBytes = hmac.ComputeHash(baseBytes);
                var hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

                return hashString == signatureToCompare;
            }
            catch
            {
                // PayMongo signature verification failed due to malformed header or cryptographic error.
                return false;
            }
        }

        private async Task LogAudit(string action)
        {
            try
            {
                var auditLog = new AuditLog
                {
                    Action = action,
                    Timestamp = DateTime.UtcNow,
                    UserID = null // System action
                };
                _db.AuditLogs.Add(auditLog);
                await _db.SaveChangesAsync();
            }
            catch 
            { 
                /* Fail silently for audit logs to avoid breaking the main webhook response */ 
            }
        }
    }
}
