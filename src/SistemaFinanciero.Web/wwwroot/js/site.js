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

// Formulario de cuentas contables: el tipo lo hereda la cuenta superior elegida. Una cuenta nueva
// dentro de Efectivo (único grupo de Activo permitido como cuenta superior, regla de creación de
// cuentas contables del bloque 3 del paquete v7) siempre es de efectivo, así que la casilla queda
// marcada y bloqueada en ese caso; en cualquier otro grupo, queda desmarcada y bloqueada. El servidor
// valida la misma regla; esto solo evita que el usuario elija combinaciones inválidas.
const ledgerParent = document.getElementById("ledger-account-parent");
const ledgerIsCash = document.getElementById("ledger-account-is-cash");
const ledgerCashFields = document.getElementById("ledger-account-cash-fields");

if (ledgerParent instanceof HTMLSelectElement) {
    const refreshLedgerForm = () => {
        const selectedOption = ledgerParent.options[ledgerParent.selectedIndex];
        const isCashGroup = selectedOption?.dataset.accountType === "Asset";

        if (ledgerIsCash instanceof HTMLInputElement) {
            // Un campo deshabilitado no se envía al servidor: solo se deshabilita cuando debe quedar
            // en falso (grupo distinto de Efectivo). Dentro de Efectivo queda marcado y habilitado,
            // porque el valor "true" sí debe llegar al servidor.
            ledgerIsCash.checked = isCashGroup;
            ledgerIsCash.disabled = !isCashGroup;
        }

        if (ledgerCashFields instanceof HTMLElement) {
            ledgerCashFields.hidden = !isCashGroup;
        }
    };

    ledgerParent.addEventListener("change", refreshLedgerForm);

    if (ledgerIsCash instanceof HTMLInputElement) {
        ledgerIsCash.addEventListener("change", refreshLedgerForm);
    }

    refreshLedgerForm();
}

// Línea de factura: la descripción siempre viene del artículo elegido, así que se muestra como vista
// previa de solo lectura en vez de capturarse. El precio y la tarifa de IVA se muestran siempre (para
// que la persona vea qué se va a facturar) y se bloquean cuando el artículo trae su propio precio de
// referencia o su tarifa predeterminada; solo quedan editables cuando el artículo no los tiene. El
// total de productos (cantidad × precio − descuento), el monto de IVA y el precio final se recalculan
// en pantalla con cada cambio; es solo una vista previa, el servidor vuelve a calcular todo al guardar.
const invoiceLineCatalogItem = document.getElementById("invoice-line-catalog-item");
const invoiceLineDescriptionPreview = document.getElementById("invoice-line-description-preview");
const invoiceLineQuantity = document.getElementById("invoice-line-quantity");
const invoiceLineUnitPrice = document.getElementById("invoice-line-unit-price");
const invoiceLineUnitPriceHint = document.getElementById("invoice-line-unit-price-hint");
const invoiceLineDiscount = document.getElementById("invoice-line-discount");
const invoiceLineTaxType = document.getElementById("invoice-line-tax-type");
const invoiceLineNetPreview = document.getElementById("invoice-line-net-preview");
const invoiceLineTaxPreview = document.getElementById("invoice-line-tax-preview");
const invoiceLineTotalPreview = document.getElementById("invoice-line-total-preview");

if (invoiceLineCatalogItem instanceof HTMLSelectElement
    && invoiceLineQuantity instanceof HTMLInputElement
    && invoiceLineUnitPrice instanceof HTMLInputElement
    && invoiceLineDiscount instanceof HTMLInputElement
    && invoiceLineTaxType instanceof HTMLSelectElement) {
    const formatCrc = (value) =>
        `₡${value.toLocaleString("es-CR", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;

    const refreshInvoiceLineTotals = () => {
        const quantity = Number(invoiceLineQuantity.value) || 0;
        const unitPrice = Number(invoiceLineUnitPrice.value) || 0;
        const discount = Number(invoiceLineDiscount.value) || 0;
        const selectedTaxOption = invoiceLineTaxType.options[invoiceLineTaxType.selectedIndex];
        const rate = Number(selectedTaxOption?.dataset.rate) || 0;

        const productsTotal = Math.max((quantity * unitPrice) - discount, 0);
        const taxAmount = productsTotal * (rate / 100);
        const finalPrice = productsTotal + taxAmount;

        if (invoiceLineNetPreview) invoiceLineNetPreview.textContent = formatCrc(productsTotal);
        if (invoiceLineTaxPreview) invoiceLineTaxPreview.textContent = formatCrc(taxAmount);
        if (invoiceLineTotalPreview) invoiceLineTotalPreview.textContent = formatCrc(finalPrice);
    };

    const refreshInvoiceLineForm = () => {
        const selectedOption = invoiceLineCatalogItem.options[invoiceLineCatalogItem.selectedIndex];
        const referencePrice = selectedOption?.dataset.referencePrice;
        const defaultTaxTypeId = selectedOption?.dataset.defaultTaxType;
        const hasReferencePrice = !!referencePrice;
        const hasDefaultTaxType = !!defaultTaxTypeId;

        if (invoiceLineDescriptionPreview) invoiceLineDescriptionPreview.value = selectedOption?.dataset.name ?? "";

        if (hasReferencePrice) {
            invoiceLineUnitPrice.value = referencePrice;
        }
        invoiceLineUnitPrice.readOnly = hasReferencePrice;
        invoiceLineUnitPrice.required = !hasReferencePrice;
        if (invoiceLineUnitPriceHint) {
            invoiceLineUnitPriceHint.textContent = hasReferencePrice
                ? "Precio de referencia del artículo; no se puede editar."
                : "El artículo no tiene un precio de referencia: indíquelo aquí.";
        }

        if (hasDefaultTaxType) {
            const matchingOption = Array.from(invoiceLineTaxType.options)
                .find((option) => option.value === defaultTaxTypeId);

            if (matchingOption) {
                invoiceLineTaxType.value = defaultTaxTypeId;
            }
        }
        invoiceLineTaxType.disabled = hasDefaultTaxType;
        invoiceLineTaxType.required = !hasDefaultTaxType;

        refreshInvoiceLineTotals();
    };

    invoiceLineCatalogItem.addEventListener("change", refreshInvoiceLineForm);
    invoiceLineQuantity.addEventListener("input", refreshInvoiceLineTotals);
    invoiceLineUnitPrice.addEventListener("input", refreshInvoiceLineTotals);
    invoiceLineDiscount.addEventListener("input", refreshInvoiceLineTotals);
    invoiceLineTaxType.addEventListener("change", refreshInvoiceLineTotals);
    refreshInvoiceLineForm();
}
