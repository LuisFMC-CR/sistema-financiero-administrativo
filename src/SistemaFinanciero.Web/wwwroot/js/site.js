// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

"use strict";

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
