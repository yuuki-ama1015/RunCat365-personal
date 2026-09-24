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
vm.runInNewContext(source.replace(/\}\)\(\);\s*$/, "globalThis.getHint = unavailableHint;\n})();"), context);
assert.equal(context.getHint("gpu", {}), "このPCでは使えません");
assert.match(context.getHint("temperature", { temperatureSetupRequired: true }), /PawnIO 2\.0/);
assert.match(context.getHint("temperature", { temperatureElevationRequired: true }), /管理者として実行/);
assert.match(context.getHint("temperature", {}), /startup\.log/);
console.log("PASS: temperature setup hints (4 cases).");
