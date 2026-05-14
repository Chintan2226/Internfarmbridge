const API = "http://127.0.0.1:8000";

const DOT_COLORS = [
    "#7F77DD", "#1D9E75", "#EF9F27", "#D85A30", "#D4537E",
    "#378ADD", "#639922", "#E24B4A", "#5DCAA5", "#BA7517"
];

let allPredictions = [];
let filteredPredictions = [];

/* ── Utilities ─────────────────────────────────── */
function apiUrl(path) {
    const sep = path.includes("?") ? "&" : "?";
    return API + path + `${sep}_=${Date.now()}`;
}

function sleep(ms) { return new Promise(r => setTimeout(r, ms)); }

async function loadDropdown(selectId, path) {
    try {
        const controller = new AbortController();
        const t = setTimeout(() => controller.abort(), 8000);
        const res = await fetch(apiUrl(path), { signal: controller.signal, cache: "no-store" });
        clearTimeout(t);
        const data = await res.json();
        const $sel = $("#" + selectId);
        $sel.find("option:not(:first)").remove();
        (data.data || []).forEach(item => {
            $sel.append($("<option>").val(item).text(item));
        });
    } catch (e) {
        console.error("Dropdown load failed:", path, e);
    }
}

async function loadDropdownWithRetry(selectId, path, tries = 3) {
    for (let i = 0; i < tries; i++) {
        await loadDropdown(selectId, path);
        const hasItems = $("#" + selectId + " option").length > 1;
        if (hasItems) return true;
        await sleep(350 * Math.pow(2, i));
    }
    return false;
}

function demandClass(label) {
    const v = (label || "").toLowerCase();
    if (v.includes("high")) return "high";
    if (v.includes("low")) return "low";
    return "med";
}

function updateKpis(rows) {
    const total = rows.length;
    const highs = rows.filter(r => (r.raw_score || 0) > 0.10).length;
    const avg = total
        ? (rows.reduce((sum, r) => sum + ((parseFloat(r.raw_score) || 0) * 100), 0) / total)
        : 0;

    $("#kpiTotal").text(total.toString());
    $("#kpiHigh").text(highs.toString());
    $("#kpiAvg").text(avg.toFixed(1) + "%");
}

/* ── Render predictions table ──────────────────── */
function initGrid() {
    if(!$("#predGrid").data("kendoGrid")) {
        $("#predGrid").kendoGrid({
            dataSource: { data: [], pageSize: 5 },
            pageable: { pageSizes: false, refresh: false, info: true, numeric: true, previousNext: true },
            sortable: true,
            columns: [
                { field: "commodity", title: "Crop", template: "<span style='font-weight:500'>#= commodity #</span>" },
                { field: "predicted_price_per_kg", title: "Predicted (₹/kg)", template: "<span class='mono'>₹#= parseFloat(predicted_price_per_kg).toFixed(2) #</span>" },
                { field: "demand_label", title: "Demand", template: function(dataItem) {
                    const cls = demandClass(dataItem.demand_label);
                    return `<span class="demand-pill ${cls}">${dataItem.demand_label || "Moderate"}</span>`;
                }}
            ]
        });
    }
}

/* ── Render demand tiles ───────────────────────── */
function renderDemand(rows) {
    const $grid = $("#demandGrid").empty();
    rows.forEach((row, i) => {
        const trend = parseFloat(row.trend_pct ?? 0);
        const up    = (row.trend_dir || "up") === "up";
        const dot   = DOT_COLORS[i % DOT_COLORS.length];
        const cls   = demandClass(row.demand_label);
        const label = row.demand_label || "Moderate";
        $grid.append(`
            <div class="demand-tile">
                <div class="demand-tile-name">
                    <span class="crop-dot" style="background:${dot}"></span>
                    ${row.commodity}
                </div>
                <div class="demand-price">₹${parseFloat(row.current_price_per_kg).toFixed(2)}/kg</div>
                <div class="demand-trend">${up ? "▲" : "▼"} ${trend.toFixed(1)}% trend</div>
                <span class="demand-badge ${cls}">${label}</span>
            </div>
        `);
    });
    $("#demandEmpty").hide();
    $grid.show();
    $("#demandCount").text(rows.length + " crops");
}

/* ── Search filter ─────────────────────────────── */
function applySearch(q) {
    filteredPredictions = q
        ? allPredictions.filter(r => r.commodity.toLowerCase().includes(q.toLowerCase()))
        : allPredictions;
    
    const grid = $("#predGrid").data("kendoGrid");
    if(grid) grid.dataSource.data(filteredPredictions);
}

/* ── Document ready ────────────────────────────── */
$(document).ready(async function () {
    initGrid();

    $("#state").prop("disabled", true);
    const ok = await loadDropdownWithRetry("state", "/dropdowns/states", 4);
    $("#state").prop("disabled", false);
    if (!ok) {
        $("#adminErr").text("AI dropdowns couldn’t load. Ensure Python service is running on port 8000.").show();
    }

    $("#state").on("change", async function () {
        const state = $(this).val();
        $("#market").find("option:not(:first)").remove();
        if (state) await loadDropdown("market", `/dropdowns/markets?state=${encodeURIComponent(state)}`);
    });

    /* Live search */
    $("#searchInput").on("input", function () {
        applySearch($(this).val());
    });

    /* Load insights */
    $("#loadBtn").on("click", async function () {
        const state  = $("#state").val();
        const market = $("#market").val();
        const $err   = $("#adminErr");

        if (!state || !market) {
            $err.text("Please select both state and market.").show();
            return;
        }

        $err.hide();
        $("#loadBtn").prop("disabled", true);
        $("#btnText").text("Loading...");
        $("#spinner").show();

        /* Skeleton loading */
        $("#tableEmpty").hide();
        $("#demandEmpty").hide();
        
        const existingGrid = $("#predGrid").data("kendoGrid");
        if (existingGrid) existingGrid.destroy();
        $("#predGrid").empty().show();
        $("#demandGrid").empty().show();

        if (typeof window.showGridSkeleton === 'function') {
            window.showGridSkeleton('#predGrid', 5);
            
            if (typeof FBSkeleton !== 'undefined') {
                FBSkeleton.show('#demandGrid', 4, 'kpi');
            } else {
                window.showGridSkeleton('#demandGrid', 3);
            }
        }

        try {
            const res  = await fetch(apiUrl("/predict/admin"), {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ state, market })
            });
            const data = await res.json();

            if (data.success) {
                allPredictions = data.all_predictions || [];
                filteredPredictions = [...allPredictions];

                /* KPIs */
                updateKpis(allPredictions);

                /* Table */
                $("#predGrid").empty(); // clear skeleton
                initGrid(); // recreate grid
                const grid = $("#predGrid").data("kendoGrid");
                grid.dataSource.data(filteredPredictions);

                /* Demand tiles */
                renderDemand(data.top_demand || []);

                /* Source tag */
                const isHistorical = allPredictions.some(x => (x.source || "").includes("historical"));
                $("#srcTag").text("Source: " + (isHistorical ? "historical/fallback" : "live/model"));

            } else {
                $err.text(data.message || "No insights found for the selected filters.").show();
            }
        } catch (e) {
            console.error("Admin insights error:", e);
            $err.text("Failed to load insights. Is the AI engine running?").show();
        } finally {
            $("#loadBtn").prop("disabled", false);
            $("#btnText").text("Load Insights");
            $("#spinner").hide();
        }
    });
});
