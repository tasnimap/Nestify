window.nestifyLanguage = {
    key: "nestify-language",
    get: function () {
        try { return localStorage.getItem(this.key); }
        catch { return null; }
    },
    apply: function (culture) {
        document.documentElement.lang = culture.toLowerCase().startsWith("bn") ? "bn-BD" : "en";
    },
    set: function (language) {
        var selected = language.toLowerCase() === "bn" ? "bn" : "en";
        localStorage.setItem(this.key, selected);
        this.apply(selected === "bn" ? "bn-BD" : "en");
    }
};
