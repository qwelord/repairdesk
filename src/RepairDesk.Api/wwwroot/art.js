const paths = {
    orders: "M5 4h14v17H5z M9 2v4m6-4v4M8 10h8m-8 4h8m-8 4h5",
    build: "m12 3 9 5-9 5-9-5zm-9 9 9 5 9-5M3 16l9 5 9-5",
    catalog: "M3 4h7v7H3zm11 0h7v7h-7zM3 15h7v6H3zm11 0h7v6h-7z",
    clients:
        "M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2M16 3a4 4 0 0 1 0 8m6 10v-2a4 4 0 0 0-3-3.87M13 7a4 4 0 1 1-8 0 4 4 0 0 1 8 0",
    services: "m14 6 4-4 4 4-4 4M4 20l12-12M3 3l4 4m-5 9 6 6",
    saved: "M5 3h14v18l-7-4-7 4z",
    compare: "M3 3v18m18-18v18M7 7h10m-3-3 3 3-3 3M7 17h10m-7-3-3 3 3 3",
    settings:
        "M12 8a4 4 0 1 1 0 8 4 4 0 0 1 0-8M12 2v3m0 14v3M2 12h3m14 0h3M5 5l2 2m10 10 2 2M5 19l2-2M17 7l2-2",
    search: "m21 21-5-5M18 10A8 8 0 1 1 2 10a8 8 0 0 1 16 0",
    plus: "M12 5v14M5 12h14",
    arrow: "M4 12h16m-6-6 6 6-6 6",
    back: "M20 12H4m6-6-6 6 6 6",
    close: "m6 6 12 12M6 18 18 6",
    logout: "M9 3H4v18h5m5-14 5 5-5 5M8 12h12",
    refresh: "M20 7v5h-5M4 17v-5h5M6 6a8 8 0 0 1 14 6M4 12a8 8 0 0 0 14 6",
    edit: "m15 4 5 5M3 21l5-1L21 7l-5-5L3 15z",
    trash: "M3 6h18M9 6V3h6v3M5 6l1 15h12l1-15M10 10v7m4-7v7",
    download: "M12 3v12m-5-5 5 5 5-5M4 17v4h16v-4",
    print: "M6 9V3h12v6M6 18H3V9h18v9h-3M6 14h12v7H6z",
    check: "m5 12 4 4L19 6",
    clock: "M12 6v6l4 2M22 12a10 10 0 1 1-20 0 10 10 0 0 1 20 0",
    external: "M14 3h7v7m-9 2 9-9M10 3H3v18h18v-7",
};

export function icon(name) {
    return `<svg class="icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="${paths[name] || paths.build}"/></svg>`;
}

const photographs = {
    ПК: ["desktop", "Собранный настольный компьютер"],
    Корпус: ["desktop", "Корпус собранного компьютера"],
    Процессор: ["cpu", "Процессор в сокете материнской платы"],
    "Материнская плата": [
        "motherboard",
        "Компоненты материнской платы крупным планом",
    ],
    Видеокарта: ["gpu", "Видеокарта с воздушным охлаждением"],
    Память: ["memory", "Модули оперативной памяти"],
    Накопитель: ["storage", "Твердотельный накопитель M.2"],
    "Блок питания": ["psu", "Компьютерный блок питания"],
    Ноутбук: ["laptop", "Открытый ноутбук"],
};

export function hardware(category = "ПК", large = false) {
    const [file, description] = photographs[category] || photographs["ПК"];
    return `<img class="hardware-photo photo-${file}" src="/assets/photos/${file}.jpg" alt="${description}" title="Фотография категории. Конкретная модель может отличаться." width="1200" height="800" loading="${large ? "eager" : "lazy"}" decoding="async">`;
}
