// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

"use strict";

const appLayout = document.getElementById("app-layout");
const sidebarToggles = document.querySelectorAll("[data-sidebar-toggle]");

const setSidebarState = (isCollapsed) => {
    if (!(appLayout instanceof HTMLElement)) {
        return;
    }

    appLayout.classList.toggle("sidebar-collapsed", isCollapsed);
    sidebarToggles.forEach((toggle) => {
        toggle.setAttribute("aria-expanded", String(!isCollapsed));
        toggle.setAttribute("aria-label", isCollapsed ? "Expandir menú" : "Contraer menú");
    });
    window.localStorage.setItem("sidebar-collapsed", String(isCollapsed));
};

if (appLayout instanceof HTMLElement) {
    const storedState = window.localStorage.getItem("sidebar-collapsed");
    setSidebarState(storedState === "true");
    sidebarToggles.forEach((toggle) => {
        toggle.addEventListener("click", () => setSidebarState(!appLayout.classList.contains("sidebar-collapsed")));
    });
}

document.querySelectorAll(".js-confirm-form").forEach((form) => {
    form.addEventListener("submit", (event) => {
        const message = form.dataset.confirmMessage ?? "¿Desea continuar?";
        if (!window.confirm(message)) {
            event.preventDefault();
        }
    });
});

const categoryKind = document.getElementById("financial-category-kind");
const categoryParent = document.getElementById("financial-category-parent");

if (categoryKind instanceof HTMLSelectElement && categoryParent instanceof HTMLSelectElement) {
    const filterParentCategories = () => {
        Array.from(categoryParent.options).forEach((option) => {
            const optionKind = option.dataset.categoryKind;
            const belongsToKind = optionKind === undefined || optionKind === categoryKind.value;
            option.hidden = !belongsToKind;
            option.disabled = !belongsToKind;

            if (!belongsToKind && option.selected) {
                categoryParent.value = "";
            }
        });
    };

    categoryKind.addEventListener("change", filterParentCategories);
    filterParentCategories();
}
