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

    const adminStorageKey = "nestify-admin-theme";
    let adminTransitionTimeout;
    let hasSavedAdminTheme;

    const updateAdminToggle = () => {
        const toggle = document.querySelector(".adm__theme-toggle");
        if (!toggle) return;

        const isDark = root.dataset.adminTheme === "dark";
        toggle.setAttribute("aria-pressed", String(isDark));
        toggle.setAttribute("aria-label", `Switch admin to ${isDark ? "light" : "dark"} mode`);
        toggle.setAttribute("title", `Switch admin to ${isDark ? "light" : "dark"} mode`);
    };

    const applyAdminTheme = (theme, animate) => {
        if (root.dataset.adminTheme !== theme && animate) {
            root.classList.add("admin-theme-transitioning");
            window.clearTimeout(adminTransitionTimeout);
            adminTransitionTimeout = window.setTimeout(
                () => root.classList.remove("admin-theme-transitioning"),
                320);
        }

        root.dataset.adminTheme = theme;
        updateAdminToggle();
    };

    const savedAdminTheme = window.localStorage.getItem(adminStorageKey);
    hasSavedAdminTheme = savedAdminTheme === "light" || savedAdminTheme === "dark";
    applyAdminTheme(
        hasSavedAdminTheme ? savedAdminTheme : preference.matches ? "dark" : "light",
        false);

    window.nestifyAdminTheme = {
        syncToggle: updateAdminToggle,
        toggle() {
            const theme = root.dataset.adminTheme === "dark" ? "light" : "dark";
            window.localStorage.setItem(adminStorageKey, theme);
            hasSavedAdminTheme = true;
            applyAdminTheme(theme, true);
        }
    };

    preference.addEventListener("change", () => {
        if (!hasSavedTheme) {
            applyTheme(preference.matches ? "dark" : "light", true);
        }

        if (!hasSavedAdminTheme) {
            applyAdminTheme(preference.matches ? "dark" : "light", true);
        }
    });
})();
