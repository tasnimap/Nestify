import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import vm from "node:vm";

const themeScript = readFileSync(
    new URL("../../src/Nestify.Web/wwwroot/js/theme.js", import.meta.url),
    "utf8");
const darkModeCss = readFileSync(
    new URL("../../src/Nestify.Web/wwwroot/css/dark-mode.css", import.meta.url),
    "utf8");
const adminCss = readFileSync(
    new URL("../../src/Nestify.Web/wwwroot/css/admin.css", import.meta.url),
    "utf8");
const themeToggle = readFileSync(
    new URL("../../src/Nestify.Web/Layout/ThemeToggle.razor", import.meta.url),
    "utf8");
const themeToggleCss = readFileSync(
    new URL("../../src/Nestify.Web/Layout/ThemeToggle.razor.css", import.meta.url),
    "utf8");

function createThemeEnvironment({ saved = {}, prefersDark = false } = {}) {
    const attributes = new Map();
    const classes = new Set();
    const listeners = [];
    const values = new Map(Object.entries(saved));
    const root = {
        dataset: {},
        classList: {
            add: (name) => classes.add(name),
            remove: (name) => classes.delete(name)
        }
    };
    const userToggle = { setAttribute: (name, value) => attributes.set(name, value) };
    const adminToggle = { setAttribute: (name, value) => attributes.set(`admin-${name}`, value) };
    const document = {
        documentElement: root,
        querySelector(selector) {
            if (selector === ".nav__theme-indicator") return userToggle;
            if (selector === ".adm__theme-toggle") return adminToggle;
            return null;
        }
    };
    const window = {
        localStorage: {
            getItem: (key) => values.get(key) ?? null,
            setItem: (key, value) => values.set(key, value)
        },
        matchMedia: () => ({
            matches: prefersDark,
            addEventListener: (event, callback) => listeners.push(callback)
        }),
        setTimeout: () => 1,
        clearTimeout: () => {}
    };

    vm.runInNewContext(themeScript, { document, window });

    return {
        root,
        attributes,
        classes,
        values,
        listeners,
        userToggle,
        adminToggle,
        window
    };
}

function contrastRatio(foreground, background) {
    const luminance = (hex) => {
        const channels = hex.match(/[a-f\d]{2}/gi).map((channel) => parseInt(channel, 16) / 255);
        const linear = channels.map((value) =>
            value <= 0.04045 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4);
        return 0.2126 * linear[0] + 0.7152 * linear[1] + 0.0722 * linear[2];
    };
    const values = [luminance(foreground), luminance(background)].sort((a, b) => b - a);
    return (values[0] + 0.05) / (values[1] + 0.05);
}

function variable(block, name) {
    const match = block.match(new RegExp(`${name}:\\s*(#[\\da-f]{6})`, "i"));
    assert.ok(match, `Expected ${name} to be defined`);
    return match[1];
}

test("device theme is the initial user and admin theme until each is overridden", () => {
    const dark = createThemeEnvironment({ prefersDark: true });
    assert.equal(dark.root.dataset.theme, "dark");
    assert.equal(dark.root.dataset.adminTheme, "dark");

    const light = createThemeEnvironment();
    assert.equal(light.root.dataset.theme, "light");
    assert.equal(light.root.dataset.adminTheme, "light");
});

test("manual theme choices persist and stop following device changes", () => {
    const environment = createThemeEnvironment();
    environment.window.nestifyTheme.toggle();
    assert.equal(environment.root.dataset.theme, "dark");
    assert.equal(environment.values.get("nestify-theme"), "dark");
    environment.listeners[0]();
    assert.equal(environment.root.dataset.theme, "dark");
});

test("user and admin theme preferences remain independent and accessible", () => {
    const environment = createThemeEnvironment();
    environment.window.nestifyTheme.toggle();
    assert.equal(environment.root.dataset.theme, "dark");
    assert.equal(environment.root.dataset.adminTheme, "light");
    environment.window.nestifyAdminTheme.toggle();
    assert.equal(environment.root.dataset.adminTheme, "dark");
    assert.equal(environment.values.get("nestify-admin-theme"), "dark");

    environment.window.nestifyTheme.syncToggle();
    environment.window.nestifyAdminTheme.syncToggle();
    assert.equal(environment.attributes.get("aria-pressed"), "true");
    assert.equal(environment.attributes.get("aria-label"), "Switch to light mode");
    assert.equal(environment.attributes.get("admin-aria-pressed"), "true");

    assert.match(themeToggle, /aria-label="Toggle color theme"/);
    assert.match(themeToggle, /aria-pressed="false"/);
    assert.match(themeToggleCss, /\.nav__theme-indicator:focus-visible/);
});

test("dark theme keeps the document canvas dark and shared role tokens readable", () => {
    const sharedTheme = darkModeCss.match(
        /html\[data-theme="dark"\]\s*\{([^}]+)\}/i)?.[1];
    const sharedRoleTokens = darkModeCss.match(
        /html\[data-theme="dark"\]\s+\.layout--user,\s*html\[data-theme="dark"\]\s+\.layout--helper\s*\{([^}]+)\}/i)?.[1];
    assert.ok(sharedTheme, "Expected root dark-theme declarations");
    assert.ok(sharedRoleTokens, "Expected shared user/helper dark-theme tokens");
    assert.match(darkModeCss, /html\[data-theme="dark"\]\s+body\s*\{\s*background:\s*var\(--nx-bg\)/i);

    const canvas = variable(sharedTheme, "--nx-bg");
    const surface = variable(sharedRoleTokens, "--nx-card");
    for (const token of ["--nx-body", "--nx-muted", "--nx-primary"]) {
        const foreground = variable(sharedRoleTokens, token);
        assert.ok(contrastRatio(foreground, canvas) >= 4.5, `${token} needs 4.5:1 contrast on canvas`);
        assert.ok(contrastRatio(foreground, surface) >= 4.5, `${token} needs 4.5:1 contrast on cards`);
    }

    for (const role of ["user", "helper"]) {
        const selector = `html[data-theme="dark"] .layout--${role} {`;
        let start = darkModeCss.indexOf(selector);
        if (role === "helper") {
            start = darkModeCss.indexOf(selector, start + selector.length);
        }
        const end = darkModeCss.indexOf("}", start);
        const roleTokens = start < 0 ? null : darkModeCss.slice(start + selector.length, end);
        assert.ok(roleTokens, `Expected ${role} accent tokens`);
        const accent = variable(roleTokens, "--nx-accent-bright");
        assert.ok(contrastRatio(accent, canvas) >= 4.5, `${role} accent needs 4.5:1 contrast on canvas`);
        assert.ok(contrastRatio(accent, surface) >= 4.5, `${role} accent needs 4.5:1 contrast on cards`);
    }
});

test("theme transitions respect reduced-motion preferences in user and helper shells", () => {
    const reducedMotion = darkModeCss.match(
        /@media\s*\(prefers-reduced-motion:\s*reduce\)\s*\{([\s\S]*?)\n\}/i)?.[1];
    assert.ok(reducedMotion, "Expected reduced-motion overrides");
    assert.match(reducedMotion, /\.layout--user/);
    assert.match(reducedMotion, /\.layout--helper/);
});

test("admin dark palette maintains readable body and muted text", () => {
    const adminTheme = adminCss.match(
        /html\[data-admin-theme="dark"\]\s+\.adm\s*\{([^}]+)\}/i)?.[1];
    assert.ok(adminTheme, "Expected admin dark-theme tokens");
    const canvas = variable(adminTheme, "--ad-canvas");
    const surface = variable(adminTheme, "--ad-surface");
    for (const token of ["--ad-body", "--ad-muted"]) {
        const foreground = variable(adminTheme, token);
        assert.ok(contrastRatio(foreground, canvas) >= 4.5, `${token} needs 4.5:1 contrast on canvas`);
        assert.ok(contrastRatio(foreground, surface) >= 4.5, `${token} needs 4.5:1 contrast on cards`);
    }
});
