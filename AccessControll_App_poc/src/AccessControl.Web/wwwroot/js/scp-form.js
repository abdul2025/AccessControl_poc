// Controller form: show only the fields that apply to the selected connection mode
// (IP Client → serial number, IP Server → IP address + port). Without JavaScript every group stays
// visible and the server validation decides. Delegated, so it also works for forms loaded into the modal.
(() => {
  "use strict";

  const apply = (form) => {
    const select = form.querySelector("[data-mode-select]");
    if (!select) {
      return;
    }

    form.querySelectorAll("[data-mode-group]").forEach((group) => {
      group.hidden = group.dataset.modeGroup !== select.value;
    });
  };

  const applyAll = (root) => {
    root.querySelectorAll("form").forEach(apply);
  };

  document.addEventListener("change", (event) => {
    if (event.target.matches("[data-mode-select]") && event.target.form) {
      apply(event.target.form);
    }
  });

  // Forms rendered into #app-modal (initial load or a validation re-render).
  const modalContent = document.querySelector("#app-modal .modal-content");
  if (modalContent) {
    new MutationObserver(() => applyAll(modalContent)).observe(modalContent, { childList: true });
  }

  applyAll(document);
})();
