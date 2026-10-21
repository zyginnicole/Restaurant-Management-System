// md-select.js – dropdowns whose list always opens BELOW the box.
// (The browser's own <select> list opens upwards when there is little room below, e.g. in a pop-up.)
//
// Use:  <select class="form-select md-input" data-md-select>…</select>
// The real <select> stays in the form (hidden) and keeps the posted value; this script draws a button
// and a list under it. Disabled options can't be picked; a disabled select / fieldset disables the button.
// After changing the select's value or options from a script, call:
//     select.dispatchEvent(new Event('md-refresh'));
(function () {
    'use strict';

    function build(select) {
        var wrap = document.createElement('div');
        wrap.className = 'md-select';
        select.parentNode.insertBefore(wrap, select);
        wrap.appendChild(select);
        select.classList.add('md-select-native');
        select.tabIndex = -1;
        select.setAttribute('aria-hidden', 'true');

        var button = document.createElement('button');
        button.type = 'button';
        button.className = 'form-select md-input md-select-button';
        button.setAttribute('aria-haspopup', 'listbox');
        button.setAttribute('aria-expanded', 'false');
        wrap.appendChild(button);

        var list = document.createElement('ul');
        list.className = 'md-select-list';
        list.setAttribute('role', 'listbox');
        list.hidden = true;
        wrap.appendChild(list);

        // The field's label opens / focuses the new button
        var label = select.id && document.querySelector('label[for="' + select.id + '"]');
        if (label) {
            if (!label.id) label.id = select.id + 'Label';
            button.setAttribute('aria-labelledby', label.id);
            label.addEventListener('click', function (e) { e.preventDefault(); button.focus(); });
        }

        function refresh() {
            var o = select.options[select.selectedIndex];
            button.textContent = o ? o.text : '';
        }

        function items() { return Array.prototype.slice.call(list.children); }

        function open() {
            if (button.matches(':disabled')) return;   // also true inside a disabled <fieldset>
            list.innerHTML = '';
            Array.prototype.forEach.call(select.options, function (o, i) {
                var li = document.createElement('li');
                li.setAttribute('role', 'option');
                li.tabIndex = -1;
                li.textContent = o.text;
                li.dataset.index = i;
                li.setAttribute('aria-selected', i === select.selectedIndex ? 'true' : 'false');
                if (o.disabled) li.setAttribute('aria-disabled', 'true');
                list.appendChild(li);
            });
            list.hidden = false;
            button.setAttribute('aria-expanded', 'true');
            var current = list.querySelector('[aria-selected="true"]') || list.querySelector('li:not([aria-disabled])');
            if (current) { current.focus(); current.scrollIntoView({ block: 'nearest' }); }
        }

        function close(focusButton) {
            if (list.hidden) return;
            list.hidden = true;
            button.setAttribute('aria-expanded', 'false');
            if (focusButton) button.focus();
        }

        function pick(li) {
            if (!li || li.hasAttribute('aria-disabled')) return;
            select.selectedIndex = +li.dataset.index;
            select.dispatchEvent(new Event('change', { bubbles: true }));
            refresh();
            close(true);
        }

        function move(step) {
            var enabled = items().filter(function (li) { return !li.hasAttribute('aria-disabled'); });
            var at = enabled.indexOf(document.activeElement);
            var next = enabled[Math.max(0, Math.min(enabled.length - 1, at + step))];
            if (next) next.focus();
        }

        button.addEventListener('click', function () { list.hidden ? open() : close(false); });
        button.addEventListener('keydown', function (e) {
            if (e.key === 'ArrowDown' || e.key === 'ArrowUp') { e.preventDefault(); open(); }
        });
        list.addEventListener('click', function (e) { pick(e.target.closest('li')); });
        list.addEventListener('keydown', function (e) {
            if (e.key === 'ArrowDown') { e.preventDefault(); move(1); }
            else if (e.key === 'ArrowUp') { e.preventDefault(); move(-1); }
            else if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); pick(document.activeElement); }
            else if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); close(true); }
            else if (e.key === 'Tab') close(false);
        });
        document.addEventListener('mousedown', function (e) { if (!wrap.contains(e.target)) close(false); });
        // Close when a pop-up around it closes
        var popup = wrap.closest('.modal');
        if (popup) popup.addEventListener('hide.bs.modal', function () { close(false); });   // Bootstrap event name

        select.addEventListener('change', refresh);
        select.addEventListener('md-refresh', function () { refresh(); close(false); });
        refresh();
    }

    document.querySelectorAll('select[data-md-select]').forEach(build);

    // For a <select data-md-select> added later (e.g. copied from a <template>): window.mdSelect(select)
    window.mdSelect = function (select) {
        if (select && !select.closest('.md-select')) build(select);
    };
})();
