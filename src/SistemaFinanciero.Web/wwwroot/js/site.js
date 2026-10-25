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

// Formulario de cuentas contables: el tipo lo hereda la cuenta superior elegida, así que los campos
// de efectivo (subtipo) solo se habilitan cuando esa cuenta superior es de tipo Activo. El servidor
// valida la misma regla; esto solo evita que el usuario elija combinaciones inválidas.
const ledgerParent = document.getElementById("ledger-account-parent");
const ledgerIsCash = document.getElementById("ledger-account-is-cash");
const ledgerCashFields = document.getElementById("ledger-account-cash-fields");

if (ledgerParent instanceof HTMLSelectElement) {
    const refreshLedgerForm = () => {
        const selectedOption = ledgerParent.options[ledgerParent.selectedIndex];
        const canBeCash = selectedOption?.dataset.accountType === "Asset";

        if (ledgerIsCash instanceof HTMLInputElement) {
            ledgerIsCash.disabled = !canBeCash;

            if (!canBeCash) {
                ledgerIsCash.checked = false;
            }
        }

        if (ledgerCashFields instanceof HTMLElement) {
            ledgerCashFields.hidden = !(ledgerIsCash instanceof HTMLInputElement && ledgerIsCash.checked);
        }
    };

    ledgerParent.addEventListener("change", refreshLedgerForm);

    if (ledgerIsCash instanceof HTMLInputElement) {
        ledgerIsCash.addEventListener("change", refreshLedgerForm);
    }

    refreshLedgerForm();
}

// Línea de factura: al elegir un producto o servicio del catálogo, se propone su tarifa de IVA
// predeterminada (RF-52). El usuario sigue pudiendo cambiarla; por eso esto solo actúa cuando el
// artículo cambia, nunca al cargar la página con una línea ya guardada.
const invoiceLineCatalogItem = document.getElementById("invoice-line-catalog-item");
const invoiceLineTaxType = document.getElementById("invoice-line-tax-type");

if (invoiceLineCatalogItem instanceof HTMLSelectElement && invoiceLineTaxType instanceof HTMLSelectElement) {
    invoiceLineCatalogItem.addEventListener("change", () => {
        const selectedOption = invoiceLineCatalogItem.options[invoiceLineCatalogItem.selectedIndex];
        const defaultTaxTypeId = selectedOption?.dataset.defaultTaxType;

        if (!defaultTaxTypeId) {
            return;
        }

        const matchingOption = Array.from(invoiceLineTaxType.options)
            .find((option) => option.value === defaultTaxTypeId);

        if (matchingOption) {
            invoiceLineTaxType.value = defaultTaxTypeId;
        }
    });
}
