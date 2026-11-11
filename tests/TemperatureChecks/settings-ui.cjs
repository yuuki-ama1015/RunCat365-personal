const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");

const source = fs.readFileSync(path.join(__dirname, "../../RunCat365/settings-ui/app.js"), "utf8");
const context = {
  document: {
    getElementById: () => ({ addEventListener() {}, setAttribute() {} }),
    querySelector: () => ({}),
    querySelectorAll: () => [],
  },
  window: { addEventListener() {} },
  location: { hash: "" },
};
vm.runInNewContext(
  source.replace(
    /\}\)\(\);\s*$/,
    "globalThis.getHint = unavailableHint;\nglobalThis.resolveRange = resolveTemperatureRange;\n})();"
  ),
  context
);
assert.equal(context.getHint("gpu", {}), "このPCでは使えません");
assert.match(context.getHint("temperature", { temperatureSetupRequired: true }), /PawnIO 2\.0/);
assert.match(context.getHint("temperature", { temperatureElevationRequired: true }), /管理者として実行/);
assert.match(context.getHint("temperature", {}), /startup\.log/);
console.log("PASS: temperature setup hints (4 cases).");

const current = {
  idleCelsius: 40,
  fullCelsius: 95,
  minimumCelsius: 0,
  maximumCelsius: 120,
  minimumSpanCelsius: 5,
};
const rangeCases = [
  ["50", "90", "idle", 50, 90],
  ["45.4", "95", "idle", 45, 95],
  ["97", "95", "idle", 97, 102],
  ["119", "95", "idle", 115, 120],
  ["-10", "95", "idle", 0, 95],
  ["40", "30", "full", 25, 30],
  ["40", "2", "full", 0, 5],
  ["40", "200", "full", 40, 120],
  ["", "80", "idle", 40, 80],
  ["50", "abc", "full", 50, 95],
];
for (const [idle, full, editedField, expectedIdle, expectedFull] of rangeCases) {
  const resolved = context.resolveRange(idle, full, editedField, current);
  assert.equal(resolved.idleCelsius, expectedIdle, `idle for ${idle}/${full}/${editedField}`);
  assert.equal(resolved.fullCelsius, expectedFull, `full for ${idle}/${full}/${editedField}`);
  assert.ok(resolved.fullCelsius - resolved.idleCelsius >= current.minimumSpanCelsius);
}
console.log(`PASS: temperature range resolution (${rangeCases.length} cases).`);
