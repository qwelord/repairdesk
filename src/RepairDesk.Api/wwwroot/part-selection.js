export function replaceCategory(parts, ids, category, id) {
    return [
        ...ids.filter(
            (value) =>
                parts.find((part) => part.id === value)?.category !== category,
        ),
        ...(id ? [id] : []),
    ];
}

export function conflicts(part, selected) {
    const others = selected.filter((item) => item.category !== part.category);
    const find = (category) =>
        part.category === category
            ? part
            : others.find((item) => item.category === category);
    const cpu = find("Процессор"),
        board = find("Материнская плата"),
        ram = find("Память");
    const gpu = find("Видеокарта"),
        psu = find("Блок питания"),
        chassis = find("Корпус");
    const reasons = [];
    const involves = (...categories) => categories.includes(part.category);
    if (
        involves("Процессор", "Материнская плата") &&
        cpu &&
        board &&
        cpu.socket !== board.socket
    )
        reasons.push("Другой сокет");
    if (
        involves("Память", "Материнская плата") &&
        board &&
        ram &&
        board.memoryType !== ram.memoryType
    )
        reasons.push("Другой тип памяти");
    if (
        involves("Память", "Материнская плата") &&
        board &&
        ram &&
        ram.capacity > board.maxMemoryGb
    )
        reasons.push("Превышен объём памяти");
    if (
        involves("Корпус", "Материнская плата") &&
        board &&
        chassis &&
        !chassis.supportedForms.split("|").includes(board.formFactor)
    )
        reasons.push("Плата не помещается");
    if (
        involves("Корпус", "Видеокарта") &&
        gpu &&
        chassis &&
        gpu.lengthMm > chassis.maxGpuMm
    )
        reasons.push("Видеокарта слишком длинная");
    if (
        involves("Блок питания", "Видеокарта") &&
        gpu &&
        psu &&
        !psu.powerConnectors.split("|").includes(gpu.powerConnectors)
    )
        reasons.push("Нет нужного питания GPU");
    if (
        involves("Процессор", "Видеокарта", "Блок питания") &&
        cpu &&
        gpu &&
        psu &&
        psu.wattage <
            Math.ceil(((cpu.wattage + gpu.wattage + 80) * 1.35) / 50) * 50
    )
        reasons.push("Недостаточно мощности БП");
    return reasons;
}

export function filterParts(
    parts,
    {
        query = "",
        brand = "",
        stockOnly = false,
        compatibleOnly = false,
        selected = [],
        descending = false,
    } = {},
) {
    const words = query
        .toLocaleLowerCase("ru")
        .trim()
        .split(/\s+/)
        .filter(Boolean);
    return parts
        .filter(
            (part) =>
                words.every((word) =>
                    `${part.brand} ${part.name} ${part.socket} ${part.memoryType} ${part.notes}`
                        .toLocaleLowerCase("ru")
                        .includes(word),
                ) &&
                (!brand || part.brand === brand) &&
                (!stockOnly || part.stock > 0) &&
                (!compatibleOnly || conflicts(part, selected).length === 0),
        )
        .sort(
            (first, second) =>
                (descending
                    ? second.price - first.price
                    : first.price - second.price) ||
                first.name.localeCompare(second.name, "ru"),
        );
}

export function specifications(part) {
    switch (part.category) {
        case "Процессор":
            return [
                part.socket,
                part.memoryType.replaceAll("|", " / "),
                `Питание до ${part.wattage} Вт`,
            ];
        case "Материнская плата":
            return [part.socket, part.memoryType, part.formFactor];
        case "Видеокарта":
            return [
                `${part.capacity} ГБ`,
                `${part.lengthMm} мм`,
                `${part.wattage} Вт`,
            ];
        case "Память":
            return [part.memoryType, `${part.capacity} ГБ`, "Комплект модулей"];
        case "Блок питания":
            return [
                `${part.wattage} Вт`,
                part.powerConnectors.includes("12VHPWR")
                    ? "Кабель 12VHPWR"
                    : "PCIe 6+2-pin",
            ];
        case "Корпус":
            return [
                `GPU до ${part.maxGpuMm} мм`,
                part.supportedForms.replaceAll("|", " / "),
            ];
        default:
            return [
                `${part.capacity} ГБ`,
                part.name.includes("SATA") ? "SATA" : "NVMe / M.2",
            ];
    }
}
