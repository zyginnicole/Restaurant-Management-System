// reservation-popup.js – shared by every reservation details pop-up (History, Reservations).
// Layout: date tile + status / day / time · guests / table, then a 2-column detail grid
// (.md-detail-grid) whose 2nd column starts in line with the guests icon above it.
//
// mdAlignDetailGrid(popupElement, container)
//   Call after the pop-up content is filled and before it is shown. It measures where the guests icon
//   sits (while the pop-up is still invisible) and starts the grid's 2nd column there, so it opens
//   already aligned. If the left text is too long to fit before that line, the normal layout is kept.
(function () {
    'use strict';

    window.mdAlignDetailGrid = function (popupElement, container) {
        var grid = container.querySelector('.md-detail-grid');
        var guests = container.querySelector('.md-detail-res .md-next-res-row .bi-person');
        if (!grid || !guests) return;

        grid.style.gridTemplateColumns = '';
        grid.style.columnGap = '';

        var wasHidden = getComputedStyle(popupElement).display === 'none';
        if (wasHidden) popupElement.style.display = 'block';      // still transparent (fade), not visible yet
        var offset = guests.getBoundingClientRect().left - grid.getBoundingClientRect().left;
        var firstWidth = grid.firstElementChild ? grid.firstElementChild.scrollWidth : 0;
        if (wasHidden) popupElement.style.display = '';

        if (offset > firstWidth + 16) {
            grid.style.columnGap = '0';
            grid.style.gridTemplateColumns = Math.round(offset) + 'px auto';
        }
    };
})();
