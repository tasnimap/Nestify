(() => {
    const root = document.documentElement;
    const preference = window.matchMedia("(prefers-color-scheme: dark)");
    const storageKey = "nestify-theme";
    let transitionTimeout;
    let hasSavedTheme;

    const updateToggle = () => {
        const toggle = document.querySelector(".nav__theme-indicator");
        if (!toggle) return;

        const isDark = root.dataset.theme === "dark";
        toggle.setAttribute("aria-pressed", String(isDark));
        toggle.setAttribute("aria-label", `Switch to ${isDark ? "light" : "dark"} mode`);
        toggle.setAttribute("title", `Switch to ${isDark ? "light" : "dark"} mode`);
    };

    const applyTheme = (theme, animate) => {
        if (root.dataset.theme !== theme && animate) {
            root.classList.add("theme-transitioning");
            window.clearTimeout(transitionTimeout);
            transitionTimeout = window.setTimeout(
                () => root.classList.remove("theme-transitioning"),
                280);
        }

        root.dataset.theme = theme;
        updateToggle();
    };

    const savedTheme = window.localStorage.getItem(storageKey);
    hasSavedTheme = savedTheme === "light" || savedTheme === "dark";
    applyTheme(hasSavedTheme ? savedTheme : preference.matches ? "dark" : "light", false);

    window.nestifyTheme = {
        syncToggle: updateToggle,
        toggle() {
            const theme = root.dataset.theme === "dark" ? "light" : "dark";
            window.localStorage.setItem(storageKey, theme);
            hasSavedTheme = true;
            applyTheme(theme, true);
        }
    };

    preference.addEventListener("change", () => {
        if (!hasSavedTheme) {
            applyTheme(preference.matches ? "dark" : "light", true);
        }
    });
})();
