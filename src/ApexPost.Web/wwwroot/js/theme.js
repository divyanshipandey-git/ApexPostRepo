(() => {
    "use strict";

    const storageKey = "apexpost-theme";
    const root = document.documentElement;
    const toggle = document.getElementById("theme-toggle");
    const supportedThemes = new Set(["light", "dark"]);

    const applyTheme = (theme, persist) => {
        if (!supportedThemes.has(theme)) return;

        root.setAttribute("data-bs-theme", theme);
        if (!toggle) return;

        const nextTheme = theme === "dark" ? "light" : "dark";
        toggle.setAttribute("aria-label", `Switch to ${nextTheme} theme`);
        toggle.setAttribute("aria-pressed", String(theme === "dark"));
        toggle.title = `Switch to ${nextTheme} theme`;

        if (persist) {
            try {
                localStorage.setItem(storageKey, theme);
            } catch {
                // Theme switching still works for this page when storage is blocked.
            }
        }
    };

    if (toggle) {
        toggle.addEventListener("click", () => {
            const current = root.getAttribute("data-bs-theme");
            applyTheme(current === "dark" ? "light" : "dark", true);
        });
    }

    window.addEventListener("storage", event => {
        if (event.key === storageKey && supportedThemes.has(event.newValue)) {
            applyTheme(event.newValue, false);
        }
    });
})();
