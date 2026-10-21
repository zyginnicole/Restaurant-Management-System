// Opens the shared notification pop-up (_NotificationPopup) when a notification is clicked.
// Each clickable item needs class "md-notice-btn" and these data-* values:
//   data-type, data-title, data-message, data-time, and optionally data-link + data-link-text.
(function () {
    var popupElement = document.getElementById('notificationPopup');
    if (!popupElement) return;

    var popup = new bootstrap.Modal(popupElement);

    document.querySelectorAll('.md-notice-btn').forEach(function (item) {
        item.addEventListener('click', function () {
            document.getElementById('notificationPopupType').textContent = item.dataset.type || 'Notification';
            document.getElementById('notificationPopupTitle').textContent = item.dataset.title || '';
            document.getElementById('notificationPopupMessage').textContent = item.dataset.message || '';
            document.getElementById('notificationPopupTime').textContent = item.dataset.time || '';

            // Action button in the footer; the footer is hidden when there is no link (close with ×)
            var link = document.getElementById('notificationPopupLink');
            var footer = document.getElementById('notificationPopupFooter');
            if (item.dataset.link) {
                link.href = item.dataset.link;
                link.textContent = item.dataset.linkText || 'View';
            }
            if (footer) footer.classList.toggle('d-none', !item.dataset.link);

            // Mark as read on screen. Saving the "read" status is for the team to add later.
            var notice = item.closest('.md-notice');
            if (notice) notice.classList.remove('unread');

            popup.show();
        });
    });
})();
