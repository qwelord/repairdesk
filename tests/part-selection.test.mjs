import { test } from "node:test";
import assert from "node:assert/strict";
import {
    filterParts,
    conflicts,
    replaceCategory,
    specifications,
} from "../src/RepairDesk.Api/wwwroot/part-selection.js";

const cpu = {
    id: 1,
    category: "Процессор",
    brand: "AMD",
    name: "Ryzen 5 5600",
    socket: "AM4",
    memoryType: "DDR4",
    wattage: 88,
    price: 10500,
    stock: 4,
    notes: "",
};
const board = {
    id: 4,
    category: "Материнская плата",
    brand: "MSI",
    name: "B550",
    socket: "AM4",
    memoryType: "DDR4",
    maxMemoryGb: 128,
    formFactor: "Micro-ATX",
    price: 11500,
    stock: 2,
    notes: "",
};
const ram = {
    id: 10,
    category: "Память",
    brand: "Kingston",
    name: "DDR4 kit",
    memoryType: "DDR4",
    capacity: 32,
    price: 6900,
    stock: 0,
    notes: "",
};
const gpu = {
    id: 7,
    category: "Видеокарта",
    brand: "MSI",
    name: "RTX 4060",
    wattage: 115,
    capacity: 8,
    lengthMm: 199,
    powerConnectors: "1×8-pin",
    price: 32900,
    stock: 4,
    notes: "",
};
const psu = {
    id: 14,
    category: "Блок питания",
    wattage: 650,
    powerConnectors: "1×8-pin|2×8-pin",
};
const chassis = {
    id: 16,
    category: "Корпус",
    maxGpuMm: 370,
    supportedForms: "ATX|Micro-ATX|Mini-ITX",
};
const selected = [cpu, board, ram, gpu, psu, chassis];

test("Search is case-insensitive, multiword, and searches sockets", () => {
    assert.deepEqual(
        filterParts([cpu, board, ram], { query: "  AMD AM4 " }).map(
            (part) => part.id,
        ),
        [1],
    );
    assert.equal(filterParts([cpu], { query: "不存在" }).length, 0);
});
test("Brand and stock filters compose", () => {
    assert.equal(
        filterParts([cpu, board, ram], { brand: "Kingston", stockOnly: true })
            .length,
        0,
    );
    assert.deepEqual(
        filterParts([cpu, board, ram], { brand: "MSI" }).map((part) => part.id),
        [4],
    );
});
test("Sorting never mutates the source", () => {
    const items = [cpu, board, ram];
    assert.deepEqual(
        filterParts(items).map((part) => part.id),
        [10, 1, 4],
    );
    assert.deepEqual(
        filterParts(items, { descending: true }).map((part) => part.id),
        [4, 1, 10],
    );
    assert.deepEqual(
        items.map((part) => part.id),
        [1, 4, 10],
    );
});
test("Replacing a category keeps the other selections and permits clearing", () => {
    const parts = [cpu, board, { ...cpu, id: 100 }];
    assert.deepEqual(
        replaceCategory(parts, [1, 4], "Процессор", 100),
        [4, 100],
    );
    assert.deepEqual(replaceCategory(parts, [1, 4], "Процессор", null), [4]);
});
test("Compatible components have no obvious conflicts", () => {
    for (const part of selected)
        assert.deepEqual(conflicts(part, selected), []);
});
test("Socket conflicts are evaluated in either direction", () => {
    assert.ok(
        conflicts({ ...cpu, socket: "AM5" }, selected).includes("Другой сокет"),
    );
    assert.ok(
        conflicts({ ...board, socket: "AM5" }, selected).includes(
            "Другой сокет",
        ),
    );
});
test("Memory type and capacity conflicts are separate", () => {
    assert.equal(
        conflicts({ ...ram, memoryType: "DDR5", capacity: 256 }, selected)
            .length,
        2,
    );
});
test("Case limits and motherboard form factors are checked", () => {
    assert.ok(
        conflicts(
            { ...chassis, maxGpuMm: 100, supportedForms: "Mini-ITX" },
            selected,
        ).length === 2,
    );
});
test("Power headroom and connectors are checked", () => {
    assert.equal(
        conflicts(
            { ...psu, wattage: 200, powerConnectors: "12VHPWR" },
            selected,
        ).length,
        2,
    );
});
test("Unrelated existing conflicts do not hide a valid RAM candidate", () => {
    assert.deepEqual(
        conflicts(ram, [cpu, { ...board, socket: "AM5" }, gpu, psu]),
        [],
    );
});
test("Compatible filter remains useful with incomplete configurations", () => {
    const incompatible = { ...cpu, id: 2, socket: "AM5" };
    assert.deepEqual(
        filterParts([cpu, incompatible], {
            compatibleOnly: true,
            selected: [board],
        }).map((part) => part.id),
        [1],
    );
    assert.equal(
        filterParts([cpu, incompatible], { compatibleOnly: true }).length,
        2,
    );
});
test("Specifications expose the relevant technical fields", () => {
    assert.ok(specifications(cpu).includes("AM4"));
    assert.ok(specifications(gpu).includes("199 мм"));
    assert.ok(specifications(chassis).includes("GPU до 370 мм"));
});
