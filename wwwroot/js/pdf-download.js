// pdf-download.js – "Download … (PDF)" buttons.
// Turns part of the page into a PDF file in the browser (html2pdf.js) and downloads it.
// The PDF shows exactly what the page shows, so once the backend fills the page with real
// data, the PDF has the real data too – no server-side PDF code needed.
//
// Usage on a button:
//   <button type="button" data-pdf-target="#confirmationCard" data-pdf-name="Reservation-R-0048">…</button>
//     data-pdf-target  CSS selector of the part to save
//     data-pdf-name    file name without ".pdf" (backend: build it from the record, e.g. the reservation code)
// Anything inside the target with data-html2canvas-ignore (e.g. the buttons, dropdowns) is left out of the PDF;
// elements with class "md-pdf-only" are hidden on screen and appear only in the PDF.
// It always downloads the file directly (no print window). If the library can't load or the PDF fails,
// the button says so for a few seconds.
(function () {
    'use strict';

    document.addEventListener('click', function (e) {
        var button = e.target.closest('[data-pdf-target]');
        if (!button) return;

        // The statistic report has a dedicated server export with real vector charts.
        // Use it directly so a large browser canvas capture cannot remain stuck on "Preparing PDF".
        if (button.dataset.pdfServer === 'true') {
            if (startServerPdf(button)) e.preventDefault();
            return;
        }

        var target = document.querySelector(button.dataset.pdfTarget);
        if (!target) return;

        if (typeof window.html2pdf !== 'function') {
            // Use the server export when the browser library is unavailable (for example, no internet).
            if (startServerPdf(button)) return;

            var original = button.innerHTML;
            button.textContent = 'PDF not available. Check your connection';
            setTimeout(function () { button.innerHTML = original; }, 3000);
            return;
        }

        var label = button.innerHTML;
        button.disabled = true;
        button.textContent = 'Preparing PDF…';

        // html2canvas shifts the capture by the page's scroll position, which leaves a blank gap at the
        // top and cuts off the bottom. Capture from the top of the page, then scroll back.
        // behavior 'instant': Bootstrap turns on smooth scrolling, which would still be moving during the capture.
        var scrollX = window.scrollX, scrollY = window.scrollY;
        window.scrollTo({ left: 0, top: 0, behavior: 'instant' });

        // Show the .md-pdf-only parts while the PDF is made, so they are measured and included (see site.css)
        var root = document.documentElement;
        root.classList.add('md-pdf-capture');
        var chrome = addPdfChrome(target);
        target.classList.add('md-pdf-document');
        var failed = false;

        window.html2pdf()
            .set({
                margin: 8,                                    // mm
                filename: buildFileName(button.dataset.pdfName) + '.pdf',
                image: { type: 'jpeg', quality: 0.98 },
                html2canvas: { scale: 2, useCORS: true, backgroundColor: '#ffffff', scrollX: 0, scrollY: 0 },
                jsPDF: { unit: 'mm', format: 'a4', orientation: 'portrait' }
            })
            .from(target)
            .save()
            .catch(function (error) {
                failed = true;
                console.error('PDF export failed.', error);
            })
            .then(function () {
                root.classList.remove('md-pdf-capture');
                target.classList.remove('md-pdf-document');
                removePdfChrome(chrome);
                window.scrollTo({ left: scrollX, top: scrollY, behavior: 'instant' });
                button.disabled = false;
                if (!failed) { button.innerHTML = label; return; }

                // The server export is the reliable fallback if html2pdf cannot capture the page.
                if (startServerPdf(button)) return;

                // No print window: say it failed on the button, then put the label back
                button.textContent = 'Couldn’t create the PDF. Try again';
                setTimeout(function () { button.innerHTML = label; }, 3000);
            });
    });

    function startServerPdf(button) {
        var fallback = button.dataset.pdfFallback;
        if (!fallback) return false;
        window.location.assign(fallback);
        return true;
    }

    function buildFileName(requestedName) {
        var systemName = document.body.dataset.systemName || 'Restaurant';
        var safeSystemName = systemName.trim()
            .replace(/[<>:"/\\|?*]/g, '-')
            .replace(/\s+/g, '-');
        var name = requestedName || 'document';

        // Avoid adding the system name twice to names already built by a page.
        if (name.toLowerCase().indexOf((safeSystemName || 'restaurant').toLowerCase() + '-') === 0) {
            return name;
        }

        // Remove old fixed prefixes from buttons that still use them.
        name = name.replace(/^(MaisonDoree|Lumiere|Lumière)-/i, '');
        return (safeSystemName || 'Restaurant') + '-' + name;
    }

    function addPdfChrome(target) {
        // Receipts already contain their own branded header and footer message.
        if (target.querySelector('.md-receipt')) return null;

        var body = document.body;
        var header = document.createElement('header');
        header.className = 'md-pdf-chrome md-pdf-header';

        var heading = document.createElement('div');
        var name = document.createElement('strong');
        name.textContent = body.dataset.systemName || 'Restaurant';
        heading.appendChild(name);

        var tagline = document.createElement('span');
        tagline.textContent = body.dataset.systemTagline || '';
        heading.appendChild(tagline);

        var contact = document.createElement('div');
        contact.textContent = [
            body.dataset.systemAddress,
            body.dataset.systemPhone,
            body.dataset.systemEmail
        ].filter(Boolean).join(' · ');
        header.appendChild(heading);
        header.appendChild(contact);

        var footer = document.createElement('footer');
        footer.className = 'md-pdf-chrome md-pdf-footer';
        footer.textContent = 'Generated on ' + new Date().toLocaleString('en-MY');

        target.prepend(header);
        target.appendChild(footer);
        return { header: header, footer: footer };
    }

    function removePdfChrome(chrome) {
        if (!chrome) return;
        chrome.header.remove();
        chrome.footer.remove();
    }
})();
