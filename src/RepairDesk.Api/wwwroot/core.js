import { icon, hardware } from "./art.js";
export { icon, hardware };
export const state = {
    user: null,
    orders: [],
    clients: [],
    parts: [],
    services: [],
    staff: [],
    reference: {},
    buildIds: [],
    currentIds: [],
    width: 1920,
    route: "orders",
    filter: "active",
    category: "Все",
    search: "",
};
export const routes = {};
export const money = (value) =>
    new Intl.NumberFormat("ru-RU", { maximumFractionDigits: 0 }).format(
        value || 0,
    ) + " ₽";
export const date = (value) =>
    new Date(value).toLocaleDateString("ru-RU", {
        day: "numeric",
        month: "short",
    });
export const esc = (value) =>
    String(value ?? "").replace(
        /[&<>"']/g,
        (char) =>
            ({
                "&": "&amp;",
                "<": "&lt;",
                ">": "&gt;",
                '"': "&quot;",
                "'": "&#39;",
            })[char],
    );
export const $ = (selector, root = document) => root.querySelector(selector);
export const $$ = (selector, root = document) => [
    ...root.querySelectorAll(selector),
];
export function button(text, action, kind = "", symbol = "") {
    return `<button type="button" class="button ${kind}" data-action="${action}">${symbol ? icon(symbol) : ""}<span>${text}</span></button>`;
}
export function badge(status) {
    return `<span class="badge ${status === "Готов" || status === "Согласована" ? "active" : status === "Отменен" || status === "Отклонена" ? "bad" : status === "Согласование" || status === "Ожидание деталей" ? "warm" : "neutral"}">${esc(status)}</span>`;
}
export function field(label, name, value = "", type = "text", extra = "") {
    return `<label class="field"><span>${label}</span><input name="${name}" type="${type}" value="${esc(value)}" ${extra}></label>`;
}
export function area(label, name, value = "", extra = "") {
    return `<label class="field ${extra}"><span>${label}</span><textarea name="${name}" maxlength="2000">${esc(value)}</textarea></label>`;
}
export function select(label, name, options, value = "", extra = "") {
    return `<label class="field ${extra}"><span>${label}</span><select name="${name}">${options
        .map((option) => {
            const [key, text] = Array.isArray(option)
                ? option
                : [option, option];
            return `<option value="${esc(key)}"${String(key) === String(value) ? " selected" : ""}>${esc(text)}</option>`;
        })
        .join("")}</select></label>`;
}
export function header(eyebrow, title, subtitle, actions = "") {
    return `<div class="page-header"><div><div class="eyebrow">${eyebrow}</div><h1>${title}</h1><p class="page-subtitle">${subtitle}</p></div><div class="page-actions">${actions}</div></div>`;
}
export function empty(title, description, action = "") {
    return `<div class="empty"><h3>${title}</h3><p>${description}</p>${action}</div>`;
}
export function toast(message, error = false) {
    const item = document.createElement("div");
    item.className = "toast" + (error ? " toast-error" : "");
    item.textContent = message;
    $("#notifications").append(item);
    setTimeout(() => item.remove(), 6500);
}
export async function api(path, method = "GET", body) {
    const response = await fetch("/api" + path, {
        method,
        credentials: "same-origin",
        headers: {
            "Content-Type": "application/json",
            "X-RepairDesk-Client": "studio",
        },
        ...(body === undefined ? {} : { body: JSON.stringify(body) }),
    });
    if (response.status === 401) {
        if (!path.startsWith("/auth/"))
            window.dispatchEvent(new Event("session-expired"));
        throw new Error("Войдите в систему.");
    }
    if (!response.ok) {
        let problem;
        try {
            problem = await response.json();
        } catch {}
        throw new Error(
            problem?.title || `Ошибка запроса (${response.status}).`,
        );
    }
    return response.status === 204
        ? null
        : response.headers.get("content-type")?.includes("json")
          ? response.json()
          : null;
}
export async function refreshData() {
    [
        state.orders,
        state.clients,
        state.parts,
        state.services,
        state.staff,
        state.reference,
    ] = await Promise.all(
        [
            "/orders",
            "/clients",
            "/parts",
            "/services",
            "/staff",
            "/reference",
        ].map((path) => api(path)),
    );
}
export async function navigate(route = state.route, id) {
    state.route = route;
    state.routeId = id;
    $$(".nav-item").forEach((item) =>
        item.classList.toggle("active", item.dataset.route === route),
    );
    const labels = {
        orders: "Заявки",
        catalog: "Комплектующие",
        build: "Студия сборки",
        clients: "Клиенты",
        services: "Работы и услуги",
        saved: "Сохранённые сборки",
        compare: "Сравнение",
        settings: "Настройки",
        order: "Карточка заявки",
        budget: "Подбор под бюджет",
    };
    $("#breadcrumb-page").textContent = labels[route] || "Мастерская";
    const content = $("#content");
    content.innerHTML =
        '<div class="loading-line"></div><div class="skeleton">Загружаем данные</div>';
    try {
        await routes[route]?.(id);
    } catch (error) {
        content.innerHTML = empty(
            "Не удалось открыть раздел",
            esc(error.message),
            button("Повторить", "reload", "", "refresh"),
        );
    }
    window.scrollTo(0, 0);
}
export function mount(html) {
    $("#content").innerHTML = html;
}
export function modal(
    title,
    content,
    onSubmit,
    { wide = false, submit = "Сохранить" } = {},
) {
    const dialog = $("#modal");
    dialog.className = wide ? "wide" : "";
    dialog.innerHTML = `<div class="modal-header"><h2 id="modal-title">${title}</h2><button class="icon-button" data-action="close-modal" aria-label="Закрыть">${icon("close")}</button></div><div class="modal-body">${onSubmit ? '<form id="modal-form">' : ""}${content}${onSubmit ? `<div class="error" id="form-error" hidden></div><div class="form-actions">${button("Отмена", "close-modal", "ghost")}<button class="button primary" type="submit">${submit}</button></div></form>` : ""}</div>`;
    if (!dialog.open) dialog.showModal();
    queueMicrotask(() =>
        ($("input,select,textarea", dialog) || $("button", dialog))?.focus(),
    );
    if (onSubmit)
        $("#modal-form").addEventListener("submit", async (event) => {
            event.preventDefault();
            const form = event.currentTarget;
            const submitButton = $("button[type=submit]", form);
            submitButton.disabled = true;
            $("#form-error").hidden = true;
            try {
                await onSubmit(Object.fromEntries(new FormData(form)), form);
                dialog.close();
            } catch (error) {
                $("#form-error").textContent = error.message;
                $("#form-error").hidden = false;
            } finally {
                submitButton.disabled = false;
            }
        });
    return dialog;
}
export async function confirmAction(title, description, action) {
    modal(
        title,
        `<p class="caption">${esc(description)}</p>`,
        async () => {
            await action();
            toast("Изменения сохранены.");
        },
        { submit: "Подтвердить" },
    );
}
export const actions = {};
document.addEventListener("click", async (event) => {
    const target = event.target.closest("[data-action],[data-route]");
    if (!target || target.disabled) return;
    event.preventDefault();
    if (target.dataset.route) {
        await navigate(target.dataset.route);
        return;
    }
    const action = target.dataset.action;
    if (action === "close-modal") {
        $("#modal").close();
        return;
    }
    if (action === "reload") {
        try {
            await refreshData();
            await navigate(state.route, state.routeId);
        } catch (error) {
            toast(error.message, true);
        }
        return;
    }
    if (actions[action]) {
        try {
            await actions[action](target, event);
        } catch (error) {
            toast(error.message, true);
        }
    }
});
export function closed(order) {
    return ["Выдан", "Отменен"].includes(order.status);
}
export function clientOptions() {
    return state.clients.map((client) => [
        client.id,
        client.name + " · " + client.phone,
    ]);
}
export function techOptions() {
    return [
        ["", "Не назначен"],
        ...state.staff
            .filter((user) => user.role === "Мастер")
            .map((user) => [user.id, user.name]),
    ];
}
export const categories = [
    "Процессор",
    "Материнская плата",
    "Видеокарта",
    "Память",
    "Блок питания",
    "Корпус",
    "Накопитель",
];
export function selectedIds() {
    return $$(".component select")
        .map((select) => Number(select.value))
        .filter(Boolean);
}
export async function afterSave(message = "Сохранено.") {
    await refreshData();
    await navigate(state.route, state.routeId);
    toast(message);
}
export async function download(path, filename) {
    const response = await fetch("/api" + path, {
        credentials: "same-origin",
        headers: { "X-RepairDesk-Client": "studio" },
    });
    if (!response.ok) {
        const problem = await response.json();
        throw new Error(problem.title || "Не удалось выгрузить файл.");
    }
    const url = URL.createObjectURL(await response.blob());
    const link = document.createElement("a");
    link.href = url;
    link.download = filename;
    document.body.append(link);
    link.click();
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 60000);
}
