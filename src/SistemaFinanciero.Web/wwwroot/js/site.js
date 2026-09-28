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
const categoryLedgerAccount = document.getElementById("financial-category-ledger-account");

if (categoryKind instanceof HTMLSelectElement && categoryParent instanceof HTMLSelectElement) {
    // La categoría padre y la cuenta contable se limitan a la naturaleza elegida (ingreso o gasto).
    const filterByKind = (select) => {
        Array.from(select.options).forEach((option) => {
            const optionKind = option.dataset.categoryKind;
            const belongsToKind = optionKind === undefined || optionKind === categoryKind.value;
            option.hidden = !belongsToKind;
            option.disabled = !belongsToKind;

            if (!belongsToKind && option.selected) {
                select.value = "";
            }
        });
    };

    const filterParentCategories = () => {
        filterByKind(categoryParent);

        if (categoryLedgerAccount instanceof HTMLSelectElement) {
            filterByKind(categoryLedgerAccount);
        }
    };

    categoryKind.addEventListener("change", filterParentCategories);
    filterParentCategories();
}

// Formulario de cuentas contables: la cuenta superior se filtra por tipo y los campos de efectivo
// (subtipo y moneda) solo se muestran para cuentas de tipo Activo marcadas como de efectivo. El
// servidor valida las mismas reglas, esto solo evita que el usuario elija combinaciones inválidas.
const ledgerType = document.getElementById("ledger-account-type");
const ledgerParent = document.getElementById("ledger-account-parent");
const ledgerIsCash = document.getElementById("ledger-account-is-cash");
const ledgerCashFields = document.getElementById("ledger-account-cash-fields");

if (ledgerType instanceof HTMLSelectElement && ledgerParent instanceof HTMLSelectElement) {
    const refreshLedgerForm = () => {
        Array.from(ledgerParent.options).forEach((option) => {
            const optionType = option.dataset.accountType;
            const belongsToType = optionType === undefined || optionType === ledgerType.value;
            option.hidden = !belongsToType;
            option.disabled = !belongsToType;

            if (!belongsToType && option.selected) {
                ledgerParent.value = "";
            }
        });

        if (ledgerIsCash instanceof HTMLInputElement) {
            const canBeCash = ledgerType.value === "Asset";
            ledgerIsCash.disabled = !canBeCash;

            if (!canBeCash) {
                ledgerIsCash.checked = false;
            }
        }

        if (ledgerCashFields instanceof HTMLElement) {
            ledgerCashFields.hidden = !(ledgerIsCash instanceof HTMLInputElement && ledgerIsCash.checked);
        }
    };

    ledgerType.addEventListener("change", refreshLedgerForm);

    if (ledgerIsCash instanceof HTMLInputElement) {
        ledgerIsCash.addEventListener("change", refreshLedgerForm);
    }

    refreshLedgerForm();
}
