// pdf-download.js – "Download … (PDF)" buttons.
// Turns part of the page into a PDF file in the browser (html2pdf.js) and downloads it.
// The PDF shows exactly what the page shows, so once the backend fills the page with real
// data, the PDF has the real data too – no server-side PDF code needed.
//
// Usage on a button:
//   <button type="button" data-pdf-target="#confirmationCard" data-pdf-name="Reservation-R-0048">…</button>
//     data-pdf-target  CSS selector of the part to save
//     data-pdf-name    file name without ".pdf" (backend: build it from the record, e.g. the reservation code)
// Anything inside the target with data-html2canvas-ignore (e.g. the buttons) is left out of the PDF.
// If the library can't load (no internet), it falls back to the browser's print dialog (Save as PDF).
(function () {
    'use strict';

    document.addEventListener('click', function (e) {
        var button = e.target.closest('[data-pdf-target]');
        if (!button) return;

        var target = document.querySelector(button.dataset.pdfTarget);
        if (!target) return;

        if (typeof window.html2pdf !== 'function') {
            window.print();
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
            .catch(function () { window.print(); })
            .then(function () {
                window.scrollTo({ left: scrollX, top: scrollY, behavior: 'instant' });
                button.disabled = false;
                button.innerHTML = label;
            });
    });
})();
