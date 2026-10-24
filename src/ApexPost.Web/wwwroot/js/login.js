(() => {
    "use strict";

    const password = document.getElementById("Password");
    const passwordToggle = document.getElementById("password-toggle");
    const form = document.getElementById("login-form");
    const submitButton = document.getElementById("login-submit");
    const status = document.getElementById("login-status");

    if (password instanceof HTMLInputElement && passwordToggle instanceof HTMLButtonElement) {
        passwordToggle.addEventListener("click", () => {
            const reveal = password.type === "password";
            password.type = reveal ? "text" : "password";
            passwordToggle.textContent = reveal ? "Hide" : "Show";
            passwordToggle.setAttribute("aria-pressed", String(reveal));
            passwordToggle.setAttribute("aria-label", reveal ? "Hide password" : "Show password");
        });
    }

    if (form instanceof HTMLFormElement && submitButton instanceof HTMLButtonElement) {
        form.addEventListener("submit", () => {
            if (!form.checkValidity()) return;

            submitButton.disabled = true;
            submitButton.setAttribute("aria-busy", "true");
            if (status instanceof HTMLElement) {
                status.textContent = "Signing in. Please wait.";
            }
            const label = submitButton.querySelector(".submit-label");
            if (label) label.textContent = "Signing in...";
        });
    }
})();
