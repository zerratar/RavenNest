// Setting a text field's value from the server, on the rare occasions the server is entitled to.
//
// This exists because the usual way of doing it is what caused the bug. An input rendered as
// value="@field" while @oninput sends every keystroke to the server means every render writes the
// server's idea of the text back into the box. Type faster than the round trip and a late render
// puts an older string back, so characters vanish while you are still typing and the box ends up
// disagreeing with the filter it drives.
//
// The fix is that the browser owns the text while somebody is typing and the server never renders
// it back. That leaves one job: clearing or presetting the field deliberately, which is this.
window.rfInput = {
    set: function (id, value) {
        try {
            var el = document.getElementById(id);
            if (!el) return;
            if (el.value === value) return;

            el.value = value == null ? '' : value;

            // Anything listening for input, including the framework's own handler, should see
            // this the same way it sees a keystroke, or the two go out of step again.
            el.dispatchEvent(new Event('input', { bubbles: true }));
        } catch (e) {
            // A field that cannot be cleared from code is still a field somebody can select and
            // delete by hand.
        }
    }
};
