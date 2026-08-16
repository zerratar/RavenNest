// Remembering whether somebody prefers wide tables as rows or as cards.
//
// One preference for the whole site rather than one per table. Somebody who wants cards on a
// phone wants them on every page, and asking them again on each one is a worse question than not
// asking.
window.rfTableView = {
    key: 'rf-table-view',

    // "cards", "table", or null for neither, which leaves it to the screen width.
    get: function () {
        try {
            var stored = window.localStorage.getItem(this.key);
            return stored === 'cards' || stored === 'table' ? stored : null;
        } catch (e) {
            // Storage blocked. Width decides, which is the sensible default anyway.
            return null;
        }
    },

    set: function (mode) {
        try {
            if (mode === 'cards' || mode === 'table') {
                window.localStorage.setItem(this.key, mode);
            } else {
                window.localStorage.removeItem(this.key);
            }
        } catch (e) {
            // The choice still applies to this page, it just will not be remembered.
        }
    }
};
