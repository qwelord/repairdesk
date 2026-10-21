import { state, $, hardware, icon, esc, money, modal, toast } from "./core.js";
import {
    replaceCategory,
    conflicts,
    filterParts,
    specifications,
} from "./part-selection.js";

export function componentRow(category, ids, context = "build") {
    const part = state.parts.find(
        (item) => ids.includes(item.id) && item.category === category,
    );
    const count = state.parts.filter(
        (item) => item.category === category,
    ).length;
    return `<button class="component-choice ${part ? "chosen" : ""}" data-action="pick-component" data-category="${esc(category)}" data-context="${context}" aria-haspopup="dialog"><span class="component-figure">${hardware(category)}</span><span class="component-copy"><span class="component-category">${esc(category)} <span>${count} вариантов</span></span><strong>${esc(part?.name || "Найти свою деталь")}</strong><span class="component-detail">${part ? `${money(part.price)} · ${part.stock > 0 ? "На складе: " + part.stock : "Под заказ"}` : "Открыть коллекцию"}</span></span><span class="component-change" aria-hidden="true">${icon(part ? "edit" : "plus")}</span></button>`;
}

export function openPicker(category, ids, onChange) {
    const items = state.parts.filter((part) => part.category === category);
    const selected = state.parts.filter((part) => ids.includes(part.id));
    const current = selected.find((part) => part.category === category);
    const filters = { selected };
    const brands = [...new Set(items.map((part) => part.brand))].sort();
    const dialog = modal(
        esc(category),
        `<div class="picker-intro"><p>Выберите деталь под свой сценарий.</p><span>${items.length} вариантов в коллекции</span></div><label class="picker-search">${icon("search")}<input id="part-search" type="search" placeholder="Название, бренд, сокет…" aria-label="Поиск комплектующей" autocomplete="off"></label><div class="picker-brands" role="group" aria-label="Производитель"><button type="button" class="active" data-brand="" aria-pressed="true">Все бренды</button>${brands.map((brand) => `<button type="button" data-brand="${esc(brand)}" aria-pressed="false">${esc(brand)}</button>`).join("")}</div><div class="picker-controls"><div><label><input id="part-stock" type="checkbox"> На складе</label><label><input id="part-compatible" type="checkbox"> Без явных конфликтов</label></div><button type="button" id="part-sort" aria-label="Изменить порядок цен">Цена: по возрастанию</button></div><div class="picker-result-line"><span id="part-count" role="status" aria-live="polite"></span>${current ? '<button type="button" id="part-remove">Убрать из конфигурации</button>' : ""}</div><div id="part-results" class="picker-grid"></div><p class="picker-footnote">Фотографии иллюстрируют категорию, не конкретную модель. Цены учебные. Полная проверка совместимости выполняется после выбора.</p>`,
        null,
        { wide: true },
    );
    dialog.classList.add("component-dialog");
    const render = () => {
        const visible = filterParts(items, filters);
        $("#part-count").textContent =
            `Показано ${visible.length} из ${items.length}`;
        $("#part-results").innerHTML = visible.length
            ? visible
                  .map((part) => {
                      const issues = conflicts(part, selected);
                      const chosen = part.id === current?.id;
                      return `<button type="button" class="picker-card ${chosen ? "selected" : ""}" data-part="${part.id}" aria-pressed="${chosen}"><span class="picker-card-top"><span>${esc(part.brand)}</span><span class="picker-stock ${part.stock ? "" : "unavailable"}">${part.stock ? "В наличии · " + part.stock : "Под заказ"}</span></span><strong>${esc(part.name)}</strong><span class="picker-specs">${specifications(
                          part,
                      )
                          .map((value) => `<span>${esc(value)}</span>`)
                          .join(
                              "",
                          )}</span><span class="picker-conflict">${issues.length ? esc(issues.join(" · ")) : "Без явных конфликтов с выбором"}</span><span class="picker-card-bottom"><b>${money(part.price)}</b><span class="picker-card-action">${chosen ? "В конфигурации" : "Выбрать"} ${icon(chosen ? "edit" : "plus")}</span></span></button>`;
                  })
                  .join("")
            : '<div class="picker-empty"><h3>Ничего не найдено</h3><p>Измените запрос или отключите фильтры.</p></div>';
    };
    $("#part-search").addEventListener("input", (event) => {
        filters.query = event.target.value;
        render();
    });
    $("#part-stock").addEventListener("change", (event) => {
        filters.stockOnly = event.target.checked;
        render();
    });
    $("#part-compatible").addEventListener("change", (event) => {
        filters.compatibleOnly = event.target.checked;
        render();
    });
    $("#part-sort").addEventListener("click", (event) => {
        filters.descending = !filters.descending;
        event.currentTarget.textContent = filters.descending
            ? "Цена: по убыванию"
            : "Цена: по возрастанию";
        render();
    });
    $(".picker-brands", dialog).addEventListener("click", (event) => {
        const target = event.target.closest("[data-brand]");
        if (!target) return;
        filters.brand = target.dataset.brand;
        for (const control of dialog.querySelectorAll("[data-brand]")) {
            const active = control === target;
            control.classList.toggle("active", active);
            control.setAttribute("aria-pressed", String(active));
        }
        render();
    });
    const choose = async (id) => {
        dialog.close();
        await onChange(replaceCategory(state.parts, ids, category, id));
    };
    $("#part-results").addEventListener("click", async (event) => {
        const card = event.target.closest("[data-part]");
        if (card) {
            try {
                await choose(Number(card.dataset.part));
            } catch (error) {
                toast(error.message, true);
            }
        }
    });
    $("#part-remove")?.addEventListener("click", async () => {
        try {
            await choose(null);
        } catch (error) {
            toast(error.message, true);
        }
    });
    render();
}
