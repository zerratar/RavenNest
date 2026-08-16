// Remembering which announcements a reader has put away.
//
// Keyed by the announcement's id rather than a single flag, so dismissing one does not also
// dismiss the next one. Reads are wrapped because localStorage throws rather than returning null
// when storage is disabled or full, and a banner is not worth taking a page down for.
window.rfDismiss = {
    isDismissed: function (key) {
        try {
            return window.localStorage.getItem('rf-dismissed-' + key) === '1';
        } catch (e) {
            return false;
        }
    },
    dismiss: function (key) {
        try {
            window.localStorage.setItem('rf-dismissed-' + key, '1');
        } catch (e) {
            // Nothing to do. It reappears next visit, which is the harmless direction to fail in.
        }
    }
};
