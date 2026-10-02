(() => {
    const root = document.documentElement;
    const storageKey = "nestify-theme";

    root.dataset.theme = localStorage.getItem(storageKey) === "dark" ? "dark" : "light";

    window.nestifyTheme = {
        isDark: () => root.dataset.theme === "dark",
        setDark: (isDark) => {
            localStorage.setItem(storageKey, isDark ? "dark" : "light");
            root.classList.add("theme-transitioning");
            root.dataset.theme = isDark ? "dark" : "light";
            window.setTimeout(() => root.classList.remove("theme-transitioning"), 280);
        }
    };
})();
