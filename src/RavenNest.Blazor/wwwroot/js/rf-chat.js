// Keeping a chat log pinned to the newest message.
//
// Blazor renders the new turn into the DOM but does not move the scroll, so a conversation past
// the height of the panel just stops appearing to grow: the answer is there, below the fold, and
// nothing says so.
window.rfChat = {
    // Only called when a turn has actually been added, so it cannot fight somebody who has
    // scrolled up to read something.
    toBottom: function (id) {
        try {
            var el = document.getElementById(id);
            if (!el) return;

            // After the browser has laid the new content out. Setting scrollTop in the same frame
            // as the render uses the old scrollHeight and lands short.
            requestAnimationFrame(function () {
                el.scrollTop = el.scrollHeight;
            });
        } catch (e) {
            // A chat that cannot scroll itself is still a usable chat.
        }
    }
};
