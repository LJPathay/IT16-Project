$(document).ready(function () {
    // Sidebar Toggle Logic
    const $sidebar = $('.sidebar');
    const $overlay = $('#sidebarOverlay');
    const $toggle = $('#sidebarToggle');

    function toggleSidebar() {
        $sidebar.toggleClass('show');
        $overlay.toggleClass('show');
        $('body').toggleClass('overflow-hidden');
    }

    $toggle.on('click', function (e) {
        e.preventDefault();
        e.stopPropagation();
        toggleSidebar();
    });

    $overlay.on('click', function () {
        toggleSidebar();
    });

    // Close sidebar on link click (for mobile)
    $sidebar.find('.nav-link').on('click', function () {
        if ($(globalThis).width() < 992) {
            toggleSidebar();
        }
    });

    // Handle Window Resize
    $(globalThis).on('resize', function () {
        if ($(globalThis).width() >= 992) {
            $sidebar.removeClass('show');
            $overlay.removeClass('show');
            $('body').removeClass('overflow-hidden');
        }
    });

    // Initialize Tooltips
    const tooltipTriggerList = Array.from(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
    const tooltipList = tooltipTriggerList.map(function (tooltipTriggerEl) {
        return new bootstrap.Tooltip(tooltipTriggerEl);
    });

    // Data Tables search styling
    $('.dataTables_filter input').addClass('form-control form-control-sm modern-form-control').attr('placeholder', 'Search...');
    $('.dataTables_length select').addClass('form-select form-select-sm modern-form-control');
});
