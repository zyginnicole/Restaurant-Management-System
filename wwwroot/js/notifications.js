(function () {
    var popupElement = document.getElementById('notificationPopup');
    if (!popupElement) return;

    var popup = new bootstrap.Modal(popupElement);

    function antiforgeryToken() {
        var input = document.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : '';
    }

    document.querySelectorAll('.md-notice-btn').forEach(function (button) {
        button.addEventListener('click', async function () {
            var notice = button.closest('.md-notice');
            var notificationId = button.dataset.id;

            document.getElementById('notificationPopupType').textContent = button.dataset.type || 'Notification';
            document.getElementById('notificationPopupTitle').textContent = button.dataset.title || '';
            document.getElementById('notificationPopupMessage').textContent = button.dataset.message || '';
            document.getElementById('notificationPopupTime').textContent = button.dataset.time || '';

            // Action button in the footer; the footer is hidden when there is no link (close with ×)
            var link = document.getElementById('notificationPopupLink');
            var footer = document.getElementById('notificationPopupFooter');
            if (button.dataset.link) {
                link.href = button.dataset.link;
                link.textContent = button.dataset.linkText || 'View';
            }
            if (footer) footer.classList.toggle('d-none', !button.dataset.link);
            if (notice) notice.classList.remove('unread');

            if (notificationId) {
                try {
                    await fetch('?handler=Read', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/x-www-form-urlencoded',
                            'RequestVerificationToken': antiforgeryToken()
                        },
                        body: new URLSearchParams({ notificationId: notificationId })
                    });
                } catch (error) {
                    console.error('Could not mark notification as read.', error);
                }
            }
            popup.show();
        });
    });
})();
