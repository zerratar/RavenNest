window.setupTextArea = (id, maxHeight) => {
  var textArea = document.getElementById(id);
  if (!textArea || typeof textArea == 'undefined') return;

  if (typeof textArea.setupCompleted == 'undefined') { 
    textArea.setupCompleted = true;
    textArea.addEventListener("input", function () {
      this.style.overflowY = 'scroll';
      if (this.scrollHeight > maxHeight) {
        this.style.overflowY = 'auto';
      } else {
        this.style.height = "auto";
        this.style.height = this.scrollHeight + "px";
        this.style.overflowY = 'hidden';
      }
    });
  }
}

function scrollToBottom(id) {
  var element = document.getElementById(id);
  if (element != null && typeof element != 'undefined') {
    element.scrollTop = element.scrollHeight;
  }
}

function dragElement(elmnt) {
  var pos1 = 0,
    pos2 = 0,
    pos3 = 0,
    pos4 = 0;
  var mdX = 0,
    mdY = 0;
  elmnt.onmousedown = dragMouseDown;
  var movingToggleButton = false;
  var elmX = 0;
  var elmY = 0;

  const elmPos = localStorage.getItem('ai-toggle-pos');

  addEventListener("resize", (event) => {

    /*make sure that the toggler is within view */
    let offset = 50;
    if (elmY < 0) { elmYelmY = 0; }
    if (elmX < 0) { elmX = 0; }
    if (elmY >= window.innerHeight - offset) { elmY = window.innerHeight - offset; }
    if (elmX >= window.innerWidth - offset) { elmX = window.innerWidth - offset; }

    // ensure the button is always within the screen
    elmnt.style.top = elmY + "px";
    elmnt.style.left = elmX + "px";
  });

  if (elmPos && elmPos.indexOf(';') > 0) {
    const d = elmPos.split(';');
    elmnt.style.top = d[0];
    elmnt.style.left = d[1];
  }

  function dragMouseDown(e) {
    movingToggleButton = false;
    e = e || window.event;
    e.preventDefault();
    // get the mouse cursor position at startup:
    mdX = pos3 = e.clientX;
    mdY = pos4 = e.clientY;

    document.onmouseup = closeDragElement;
    // call a function whenever the cursor moves:
    document.onmousemove = elementDrag;
  }

  function elementDrag(e) {
    e = e || window.event;
    e.preventDefault();
    let offset = 50;
    // calculate the new cursor position:
    pos1 = pos3 - e.clientX;
    pos2 = pos4 - e.clientY;
    pos3 = e.clientX;
    pos4 = e.clientY;
    // set the element's new position:
    let newTop = (elmnt.offsetTop - pos2);
    let newLeft = (elmnt.offsetLeft - pos1);
    if (newLeft < 0) { newLeft = 0; }
    if (newTop < 0) { newTop = 0; }
    if (newTop >= window.innerHeight - offset) { newTop = window.innerHeight - offset; }
    if (newLeft >= window.innerWidth - offset) { newLeft = window.innerWidth - offset; }

    elmY = newTop;
    elmX = newLeft;

    elmnt.style.top = newTop + "px";
    elmnt.style.left = newLeft + "px";
    movingToggleButton = true;

    localStorage.setItem('ai-toggle-pos', elmnt.style.top + ';' + elmnt.style.left);
  }

  function closeDragElement(e) {
    e = e || window.event;
    let dx = mdX - e.clientX;
    let dy = mdY - e.clientY;

    // to ensure that you can accidently just move slightly when
    // intention is to open the extension
    if (Math.abs(dx) <= 2 && Math.abs(dy) <= 2) {
      movingToggleButton = false;
    }

    // stop moving when mouse button is released:
    document.onmouseup = null;
    document.onmousemove = null;
  }
}
