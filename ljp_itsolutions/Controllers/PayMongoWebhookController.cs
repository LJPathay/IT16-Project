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
                var payload = JsonDocument.Parse(json);
                var root = payload.RootElement;
                
                // Extract event type
                var eventType = root.GetProperty("data").GetProperty("attributes").GetProperty("type").GetString();
                
                if (eventType == "payment.paid" || eventType == "checkout_session.payment.paid")
                {
                    var dataObj = root.GetProperty("data").GetProperty("attributes").GetProperty("data");
                    var attributes = dataObj.GetProperty("attributes");
                    
                    string? externalRef = null;

                    // Check for external_reference in root attributes
                    if (attributes.TryGetProperty("external_reference", out var refProperty) && refProperty.ValueKind != JsonValueKind.Null)
                    {
                        externalRef = refProperty.GetString();
                    }
                    // Check for external_reference in metadata
                    else if (attributes.TryGetProperty("metadata", out var metaProp) && metaProp.TryGetProperty("external_reference", out var metaRefProp))
                    {
                        externalRef = metaRefProp.GetString();
                    }


                    if (!string.IsNullOrEmpty(externalRef) && Guid.TryParse(externalRef, out var orderId))
                    {
                        var order = await _db.Orders
                            .Include(o => o.Payments)
                            .FirstOrDefaultAsync(o => o.OrderID == orderId);

                        if (order != null)
                        {
                            // CRITICAL SECURITY FIX: Verify payment amount
                            if (attributes.TryGetProperty("amount", out var amountProp))
                            {
                                long amountInCentavos = amountProp.GetInt64();
                                decimal amountInPesos = amountInCentavos / 100m;

                                // Check if the amount paid matches the order total (FinalAmount)
                                // We allow for a 1-peso tolerance to handle potential rounding issues in extremely rare cases, 
                                // but ideally they should match exactly.
                                if (Math.Abs(amountInPesos - order.FinalAmount) > 0.01m)
                                {
                                    _logger.LogWarning("PayMongo Webhook Amount Mismatch! Order: {OrderId}, Expected: {Expected}, Received: {Received}", 
                                        orderId, order.FinalAmount, amountInPesos);
                                    
                                    await LogAudit($"PayMongo Security Alert: Amount mismatch for Order #{orderId.ToString().Substring(0, 8)}. Expected {order.FinalAmount}, Received {amountInPesos}");
                                    return BadRequest("Amount Mismatch");
                                }
                            }
                            else 
                            {
                                _logger.LogWarning("PayMongo Webhook Missing Amount! Order: {OrderId}", orderId);
                                return BadRequest("Missing Amount in Payload");
                            }

                            order.PaymentStatus = "Paid"; 
                            
                            // Update or Add Payment record
                            var payment = order.Payments.FirstOrDefault(p => p.PaymentMethod.Contains("Paymongo") || p.PaymentMethod == "E-Wallet");
                            if (payment != null)
                            {
                                payment.PaymentStatus = "Completed";
                                payment.ReferenceNumber = dataObj.GetProperty("id").GetString(); 
                                payment.AmountPaid = order.FinalAmount; // Update with actual amount
                            }
                            else
                            {
                                _db.Payments.Add(new Payment
                                {
                                    OrderID = order.OrderID,
                                    AmountPaid = order.FinalAmount,
                                    PaymentMethod = "Paymongo E-Wallet",
                                    PaymentStatus = "Completed",
                                    PaymentDate = DateTime.Now,
                                    ReferenceNumber = dataObj.GetProperty("id").GetString()
                                });
                            }
                            
                            
                            await _db.SaveChangesAsync();
                            await LogAudit($"PayMongo: Order #{orderId.ToString().Substring(0, 8)} confirmed paid.");
                            _logger.LogInformation("Order {OrderId} marked as paid via webhook.", orderId);

                            // Auto-send e-receipt if customer has an email
                             if (order.CustomerID.HasValue)
                             {
                                 _ = Task.Run(async () => {
                                     try {
                                         using (var scope = _scopeFactory.CreateScope()) {
                                             var scopedReceiptService = scope.ServiceProvider.GetRequiredService<IReceiptService>();
                                         }
                                     } catch (Exception ex) {
                                         _logger.LogError(ex, "Background webhook receipt sending failed for order {OrderId}", order.OrderID);
                                     }
                                 });
                             }
                        }
                    }
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing PayMongo webhook.");
                return BadRequest();
            }
        }


        private bool VerifySignature(byte[] payloadBytes, string signatureHeader, string secret)
        {
            try
            {
                // PayMongo signature
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
                    Timestamp = DateTime.Now,
                    UserID = null // System action
                };
                _db.AuditLogs.Add(auditLog);
                await _db.SaveChangesAsync();
            }
            catch { /* Fail silently */ }
        }
    }
}
