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

        var target = document.querySelector(button.dataset.pdfTarget);
        if (!target) return;

        if (typeof window.html2pdf !== 'function') {
            // The PDF library didn't load (e.g. no internet): say so instead of opening a print window
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
        var failed = false;

        window.html2pdf()
            .set({
                margin: 12,                                   // mm
                filename: (button.dataset.pdfName || 'MaisonDoree') + '.pdf',
                image: { type: 'jpeg', quality: 0.98 },
                html2canvas: { scale: 2, useCORS: true, backgroundColor: '#ffffff', scrollX: 0, scrollY: 0 },
                jsPDF: { unit: 'mm', format: 'a4', orientation: 'portrait' }
            })
            .from(target)
            .save()
            .catch(function () { failed = true; })
            .then(function () {
                root.classList.remove('md-pdf-capture');
                window.scrollTo({ left: scrollX, top: scrollY, behavior: 'instant' });
                button.disabled = false;
                if (!failed) { button.innerHTML = label; return; }
                // No print window: say it failed on the button, then put the label back
                button.textContent = 'Couldn’t create the PDF. Try again';
                setTimeout(function () { button.innerHTML = label; }, 3000);
            });
    });
})();
