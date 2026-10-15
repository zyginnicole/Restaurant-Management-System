// Opens the shared notification pop-up (_NotificationModal) when a notification is clicked.
// Each clickable item needs class "md-notice-btn" and these data-* values:
//   data-type, data-title, data-message, data-time, and optionally data-link + data-link-text.
(function () {
    var modalElement = document.getElementById('notificationModal');
    if (!modalElement) return;

    var modal = new bootstrap.Modal(modalElement);

    document.querySelectorAll('.md-notice-btn').forEach(function (item) {
        item.addEventListener('click', function () {
            document.getElementById('notificationModalType').textContent = item.dataset.type || 'Notification';
            document.getElementById('notificationModalTitle').textContent = item.dataset.title || '';
            document.getElementById('notificationModalMessage').textContent = item.dataset.message || '';
            document.getElementById('notificationModalTime').textContent = item.dataset.time || '';

            var link = document.getElementById('notificationModalLink');
            if (item.dataset.link) {
                link.href = item.dataset.link;
                link.textContent = item.dataset.linkText || 'View';
                link.classList.remove('d-none');
            } else {
                link.classList.add('d-none');
            }

            // Mark as read on screen. Saving the "read" status is for the team to add later.
            var notice = item.closest('.md-notice');
            if (notice) notice.classList.remove('unread');

            modal.show();
        });
    });
})();
