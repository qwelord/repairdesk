import {
    state,
    $,
    $$,
    icon,
    hardware,
    esc,
    api,
    refreshData,
    navigate,
    mount,
    modal,
    toast,
    button,
    header,
    field,
    select,
    area,
    actions,
    routes,
    money,
    date,
    empty,
    categories,
    confirmAction,
} from "./core.js";
routes.catalog = () => {
    mount(
        header(
            "Библиотека компонентов",
            "Каждая деталь имеет значение.",
            "Характеристики, совместимость и наличие. Каталог, с которым удобно работать.",
            `${button("Движения", "stock-history", "", "clock")}${state.user.canManage ? button("Комплектующая", "new-part", "primary", "plus") : ""}`,
        ) +
            `<div class="toolbar"><div class="category-strip">${["Все", ...categories].map((category) => `<button class="tab ${state.category === category ? "active" : ""}" data-action="category" data-category="${category}">${category}</button>`).join("")}</div><input id="part-search" class="filter-input" type="search" aria-label="Поиск комплектующих" placeholder="Поиск по названию…" value="${esc(state.search)}"></div><div id="catalog-results"></div><p class="footer-note">Цены учебного каталога не являются актуальными рыночными ценами. Источник характеристик указан в карточке.</p>`,
    );
    $("#part-search").addEventListener("input", () => {
        state.search = $("#part-search").value;
        renderCatalog();
    });
    renderCatalog();
};
function specs(part) {
    return [
        part.socket,
        part.memoryType,
        part.capacity
            ? part.capacity +
              (part.category === "Видеокарта" || part.category === "Память"
                  ? " ГБ"
                  : " ГБ")
            : null,
        part.wattage ? part.wattage + " Вт" : null,
        part.formFactor,
        part.lengthMm ? part.lengthMm + " мм" : null,
    ]
        .filter(Boolean)
        .join(" / ");
}
function renderCatalog() {
    const items = state.parts.filter(
        (part) =>
            (state.category === "Все" || part.category === state.category) &&
            part.name.toLowerCase().includes(state.search.toLowerCase()),
    );
    $("#catalog-results").innerHTML = items.length
        ? `<div class="catalog-grid">${items.map((part) => `<button class="product" data-action="part" data-id="${part.id}"><div class="product-visual"><span class="eyebrow">${esc(part.brand)} / ${esc(part.category)}</span>${hardware(part.category)}</div><div class="product-body"><div class="eyebrow">${esc(part.category)}</div><h3>${esc(part.name)}</h3><div class="product-spec">${esc(specs(part)) || "Характеристики в карточке"}</div><div class="product-bottom"><span class="product-price">${money(part.price)}</span><span class="stock-pill ${part.stock === 0 ? "none" : part.stock <= part.minStock ? "low" : ""}">${part.stock ? part.stock + " на складе" : "Под заказ"}</span></div></div></button>`).join("")}</div>`
        : empty(
              "Ничего не найдено",
              "Измените категорию или поисковый запрос.",
          );
}
actions.category = (target) => {
    state.category = target.dataset.category;
    routes.catalog();
};
actions.part = (target) => {
    const part = state.parts.find(
        (item) => item.id === Number(target.dataset.id),
    );
    modal(
        esc(part.name),
        `<div class="product-visual">${hardware(part.category)}</div><div class="row between"><div><div class="eyebrow">${esc(part.brand)} / ${part.category}</div><div class="price-total">${money(part.price)}</div></div><span class="stock-pill ${part.stock ? "" : "none"}">${part.stockText}</span></div><div class="divider"></div><dl class="detail-facts">${Object.entries(
            {
                Сокет: part.socket,
                "Тип памяти": part.memoryType,
                Мощность: part.wattage ? part.wattage + " Вт" : "",
                Объём: part.capacity ? part.capacity + " ГБ" : "",
                "Длина GPU": part.lengthMm ? part.lengthMm + " мм" : "",
                "Форм-фактор": part.formFactor,
                "Поддерживаемые платы": part.supportedForms,
                "GPU до": part.maxGpuMm ? part.maxGpuMm + " мм" : "",
                "Память до": part.maxMemoryGb ? part.maxMemoryGb + " ГБ" : "",
                "Поддержка CPU": part.cpuSupport,
                "Разъёмы питания": part.powerConnectors,
            },
        )
            .filter(([, value]) => value)
            .map(
                ([key, value]) =>
                    `<div><dt>${key}</dt><dd>${esc(value)}</dd></div>`,
            )
            .join(
                "",
            )}</dl><p class="caption">${esc(part.notes)}</p>${part.sourceUrl ? `<p class="caption"><a href="${esc(part.sourceUrl)}" target="_blank" rel="noopener noreferrer">Характеристики производителя ↗</a></p>` : ""}<div class="form-actions wrap">${state.user.canManage ? `${button("Удалить", "delete-part", "small danger", "trash")}${button("Изменить", "edit-part", "small", "edit")}` : ""}${state.user.canReceive ? button("Движение склада", "move-part", "primary") : ""}</div>`,
    );
    for (const action of ["delete-part", "edit-part", "move-part"]) {
        const control = $(`[data-action="${action}"]`);
        if (control) control.dataset.id = part.id;
    }
};
actions["delete-part"] = (target) =>
    confirmAction(
        "Удалить комплектующую?",
        "Удаляется только неиспользуемая позиция с нулевым остатком. История склада и заказов защищена от удаления.",
        async () => {
            await api("/parts/" + target.dataset.id, "DELETE");
            await refreshData();
            await navigate("catalog");
        },
    );
actions["move-part"] = (target) => {
    const part = state.parts.find(
        (item) => item.id === Number(target.dataset.id),
    );
    modal(
        "Движение по складу",
        `<p class="caption">${esc(part.name)} · сейчас ${part.stock} шт.</p><div class="stack">${field("Изменение количества", "quantity", 1, "number", 'required min="-10000" max="10000"')} ${field("Причина движения", "reason", "", "text", 'required maxlength="250"')}<p class="help">Положительное число — приход, отрицательное — списание. Остаток не может стать отрицательным.</p></div>`,
        async (data) => {
            await api("/parts/" + part.id + "/movements", "POST", {
                quantity: Number(data.quantity),
                reason: data.reason,
            });
            await refreshData();
            await navigate("catalog");
            toast("Движение записано.");
        },
    );
};
actions["new-part"] = () => editPart();
actions["edit-part"] = (target) =>
    editPart(state.parts.find((item) => item.id === Number(target.dataset.id)));
function editPart(part) {
    const defaults = {
        id: 0,
        category: "Процессор",
        brand: "",
        name: "",
        price: 0,
        stock: 0,
        minStock: 1,
        socket: "",
        memoryType: "",
        wattage: 0,
        capacity: 0,
        lengthMm: 0,
        formFactor: "",
        supportedForms: "",
        maxGpuMm: 0,
        maxMemoryGb: 0,
        cpuSupport: "",
        powerConnectors: "",
        sourceUrl: "",
        notes: "",
    };
    const value = { ...defaults, ...part };
    modal(
        part ? "Изменить комплектующую" : "Новая комплектующая",
        `<div class="form-grid">${select("Категория", "category", categories, value.category)}${field("Производитель", "brand", value.brand, "text", 'required maxlength="60"')}${field("Наименование", "name", value.name, "text", 'required maxlength="160"')}${field("Цена, ₽", "price", value.price, "number", 'required min="0" max="10000000" step="0.01"')}${field("Минимальный остаток", "minStock", value.minStock, "number", 'required min="0" max="10000"')}${field("Сокет", "socket", value.socket, "text", 'maxlength="30"')}${select("Тип памяти", "memoryType", ["", "DDR4", "DDR5", "DDR4|DDR5"], value.memoryType)}${field("Мощность / пиковая мощность, Вт", "wattage", value.wattage, "number", 'min="0" max="100000"')}${field("Объём памяти / накопителя, ГБ", "capacity", value.capacity, "number", 'min="0" max="100000"')}${field("Длина видеокарты, мм", "lengthMm", value.lengthMm, "number", 'min="0" max="100000"')}${field("Форм-фактор платы", "formFactor", value.formFactor, "text", 'maxlength="30"')}${field("Форматы корпуса через |", "supportedForms", value.supportedForms, "text", 'maxlength="100"')}${field("Максимальная длина GPU, мм", "maxGpuMm", value.maxGpuMm, "number", 'min="0" max="100000"')}${field("Максимальная память платы, ГБ", "maxMemoryGb", value.maxMemoryGb, "number", 'min="0" max="100000"')}${field("Разъёмы PCIe питания через |", "powerConnectors", value.powerConnectors, "text", 'maxlength="100"')}${field("Поддерживаемые процессоры", "cpuSupport", value.cpuSupport, "text", 'maxlength="4000"')}${field("HTTPS-ссылка производителя", "sourceUrl", value.sourceUrl, "url", 'maxlength="600"')}${area("Примечания", "notes", value.notes, "full")}</div><p class="help">Поля зависят от категории. Для CPU указывайте пиковую мощность, для БП — номинальную. Остаток изменяется отдельно через движение склада, а не редактированием карточки.</p>`,
        async (data) => {
            for (const key of [
                "price",
                "minStock",
                "wattage",
                "capacity",
                "lengthMm",
                "maxGpuMm",
                "maxMemoryGb",
            ])
                data[key] = Number(data[key]);
            await api(
                part ? "/parts/" + part.id : "/parts",
                part ? "PUT" : "POST",
                { ...value, ...data },
            );
            await refreshData();
            await navigate("catalog");
            toast("Комплектующая сохранена.");
        },
        { wide: true },
    );
}
actions["stock-history"] = async () => {
    const moves = await api("/stock-movements");
    modal(
        "Движения склада",
        moves.length
            ? `<div class="table-wrap"><table><thead><tr><th>Когда / кто</th><th>Деталь</th><th>Кол-во</th><th>Причина</th></tr></thead><tbody>${moves.map((move) => `<tr><td>${date(move.createdAt)}<div class="secondary-text">${esc(move.author)}</div></td><td>${esc(move.part)}</td><td>${move.quantity > 0 ? "+" : ""}${move.quantity}</td><td>${esc(move.reason)}</td></tr>`).join("")}</tbody></table></div>`
            : empty(
                  "Движений пока нет",
                  "История пополнений и списаний появится здесь.",
              ),
        null,
        { wide: true },
    );
};

routes.clients = () => {
    mount(
        header(
            "Люди мастерской",
            "Сервис начинается с человека.",
            "Контакты, заметки и история техники — без потерянных договорённостей.",
            state.user.canReceive
                ? button("Новый клиент", "new-client", "primary", "plus")
                : "",
        ) +
            `<section class="workspace"><div class="toolbar"><h3>Клиенты <span class="muted">/ ${state.clients.length}</span></h3><input id="client-search" type="search" class="filter-input" placeholder="Имя или телефон…" aria-label="Поиск клиентов"></div><div id="clients-table"></div></section>`,
    );
    $("#client-search").addEventListener("input", renderClients);
    renderClients();
};
function renderClients() {
    const query = $("#client-search").value.toLowerCase();
    const clients = state.clients.filter((client) =>
        (client.name + " " + client.phone + " " + client.email)
            .toLowerCase()
            .includes(query),
    );
    $("#clients-table").innerHTML = clients.length
        ? `<div class="table-wrap"><table><thead><tr><th>Клиент</th><th>Контакты</th><th>Техника в мастерской</th><th>Примечание</th><th></th></tr></thead><tbody>${clients.map((client) => `<tr class="clickable" data-action="client" data-id="${client.id}"><td><div class="row"><span class="user-avatar">${esc(client.name[0])}</span><span>${esc(client.name)}</span></div></td><td>${esc(client.phone)}<div class="secondary-text">${esc(client.email) || "Почта не указана"}</div></td><td>${state.orders.filter((order) => order.clientId === client.id).length} заявок</td><td><div class="primary-text">${esc(client.notes) || "—"}</div></td><td>${icon("arrow")}</td></tr>`).join("")}</tbody></table></div>`
        : empty(
              "Клиентов пока нет",
              "Добавьте первого клиента для оформления заявки.",
          );
}
actions["new-client"] = () => editClient();
function editClient(client) {
    modal(
        client ? "Карточка клиента" : "Новый клиент",
        `<div class="form-grid">${field("Имя и фамилия", "name", client?.name || "", "text", 'required maxlength="120"')}${field("Телефон", "phone", client?.phone || "", "tel", 'required maxlength="40"')}${field("Электронная почта", "email", client?.email || "", "email", 'maxlength="180"')}${area("Примечание", "notes", client?.notes || "", "full")}</div>`,
        async (data) => {
            await api(
                client ? "/clients/" + client.id : "/clients",
                client ? "PUT" : "POST",
                data,
            );
            await refreshData();
            await navigate("clients");
            toast("Контакты сохранены.");
        },
    );
}
actions.client = (target) => {
    const client = state.clients.find(
        (item) => item.id === Number(target.dataset.id),
    );
    const orders = state.orders.filter((order) => order.clientId === client.id);
    modal(
        esc(client.name),
        `<p class="caption">${esc(client.phone)}<br>${esc(client.email)}<br>${esc(client.notes)}</p><h3>История техники</h3><div class="search-results">${orders.map((order) => `<button class="search-result" data-action="client-order" data-id="${order.id}">${icon("orders")}<div><strong>${esc(order.device)}</strong><small>${order.number} · ${esc(order.status)}</small></div></button>`).join("") || '<p class="caption">Устройства ещё не принимались.</p>'}</div>${state.user.canReceive ? `<div class="form-actions">${button("Удалить", "delete-client", "danger small", "trash")}${button("Изменить", "edit-client", "primary small", "edit")}</div>` : ""}`,
    );
    for (const action of ["delete-client", "edit-client"]) {
        const control = $(`[data-action="${action}"]`);
        if (control) control.dataset.id = client.id;
    }
};
actions["client-order"] = async (target) => {
    $("#modal").close();
    await navigate("order", Number(target.dataset.id));
};
actions["edit-client"] = (target) =>
    editClient(
        state.clients.find((item) => item.id === Number(target.dataset.id)),
    );
actions["delete-client"] = (target) =>
    confirmAction(
        "Удалить клиента?",
        "Контакт можно удалить, только если он не используется в заявках и сборках.",
        async () => {
            await api("/clients/" + target.dataset.id, "DELETE");
            await refreshData();
            await navigate("clients");
        },
    );
routes.services = () =>
    mount(
        header(
            "Каталог мастерской",
            "Работа, которая ценится.",
            "Понятные услуги и фиксированные цены. В заказе сохраняется цена на момент добавления.",
            state.user.canManage
                ? button("Новая услуга", "new-service", "primary", "plus")
                : "",
        ) +
            `<section class="workspace"><div class="table-wrap"><table><thead><tr><th>Работа</th><th>Стоимость</th><th></th></tr></thead><tbody>${state.services.map((service) => `<tr><td><div class="row">${icon("services")}<span>${esc(service.name)}</span></div></td><td>${money(service.price)}</td><td>${state.user.canManage ? `<div class="line-actions"><button class="icon-button" data-action="edit-service" data-id="${service.id}" aria-label="Изменить услугу">${icon("edit")}</button><button class="icon-button" data-action="delete-service" data-id="${service.id}" aria-label="Удалить услугу">${icon("trash")}</button></div>` : ""}</td></tr>`).join("")}</tbody></table></div></section>`,
    );
function editService(service) {
    modal(
        service ? "Изменить услугу" : "Новая услуга",
        `<div class="stack">${field("Наименование", "name", service?.name || "", "text", 'required maxlength="160"')}${field("Стоимость, ₽", "price", service?.price || 0, "number", 'required min="0" max="10000000" step="0.01"')}</div>`,
        async (data) => {
            await api(
                service ? "/services/" + service.id : "/services",
                service ? "PUT" : "POST",
                { name: data.name, price: Number(data.price) },
            );
            await refreshData();
            await navigate("services");
            toast("Услуга сохранена.");
        },
    );
}
actions["new-service"] = () => editService();
actions["edit-service"] = (target) =>
    editService(
        state.services.find((item) => item.id === Number(target.dataset.id)),
    );
actions["delete-service"] = (target) =>
    confirmAction(
        "Удалить услугу?",
        "Уже добавленные в заказы позиции и их стоимость не изменятся.",
        async () => {
            await api("/services/" + target.dataset.id, "DELETE");
            await refreshData();
            await navigate("services");
        },
    );
