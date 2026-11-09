// Shared customer chatbot used by the menu and dashboard pages.
(function () {
    var toggle = document.getElementById('chatToggle');
    var panel = document.getElementById('chatPanel');
    var close = document.getElementById('chatClose');
    var form = document.getElementById('chatForm');
    var input = document.getElementById('chatText');
    var body = document.getElementById('chatBody');

    // The script is also safe on pages that do not include the chatbot markup.
    if (!toggle || !panel || !close || !form || !input || !body) return;

    // Reinforce the server-side limit at the input layer as well.
    input.maxLength = 300;

    function setChat(open) {
        panel.classList.toggle('d-none', !open);
        toggle.classList.toggle('d-none', open);
        toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
        if (open) input.focus();
    }

    toggle.addEventListener('click', function () { setChat(true); });
    close.addEventListener('click', function () { setChat(false); });

    form.addEventListener('submit', async function (event) {
        event.preventDefault();

        var text = input.value.trim().slice(0, 300);
        if (!text) return;

        var message = document.createElement('div');
        message.className = 'md-chat-msg user';
        message.textContent = text;
        body.appendChild(message);
        input.value = '';

        var reply = document.createElement('div');
        reply.className = 'md-chat-msg bot';
        reply.textContent = 'Thinking...';
        body.appendChild(reply);
        body.scrollTop = body.scrollHeight;

        try {
            var response = await fetch('/api/chat', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ message: text })
            });

            if (!response.ok) throw new Error('Chat request failed.');

            var data = await response.json();
            reply.textContent = data.reply || 'I could not generate a reply.';

            if (data.linkUrl) {
                var link = document.createElement('a');
                link.href = data.linkUrl;
                link.textContent = data.linkText || 'Open page';
                link.className = 'btn btn-sm btn-md-outline md-chat-link';
                reply.appendChild(document.createElement('br'));
                reply.appendChild(link);
            }
        } catch (error) {
            console.error(error);
            reply.textContent = 'The assistant is temporarily unavailable.';
        }

        body.scrollTop = body.scrollHeight;
    });
})();
