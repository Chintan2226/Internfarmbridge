/**
 * Farmer Crops — crops.js
 * JWT Bearer token is read from cookie: "authToken"
 * Multi-farmer support enabled
 */

// AUTH HELPERS (module scope)

function getToken() {
    var match = document.cookie.match(/(?:^|;\s*)authToken=([^;]+)/);
    return match ? decodeURIComponent(match[1]) : null;
}

function authHeaders() {
    var token = getToken();
    if (!token) { redirectToLogin(); return {}; }
    return { "Authorization": "Bearer " + token };
}

function handleUnauthorized(xhr) {
    if (!xhr || xhr.status === 401 || xhr.status === 403 || !getToken()) {
        redirectToLogin();
        return true;
    }
    return false;
}

function redirectToLogin() {
    window.location.href = "/Farmer/Login";
}

// ROW MAPPER — maps API item to Kendo template data object

function mapListingToRow(item) {
    return {
        Id: item.listingId,
        Crop: item.cropName || "—",
        Variety: item.variety || "Standard",
        Qty: item.availableQuantity || 0,
        Unit: item.unit || "kg",
        Price: item.askingPrice || 0,
        HarvestDate: kendo.toString(new Date(item.createdAt), "dd MMM yyyy"),
        Status: item.status || "draft",
        FarmDistrict: item.farmDistrict || item.district || "—",
        FarmState: item.farmState || item.state || "—",
        Notes: item.notes || ""
    };
}

// STATS BAR UPDATER

function updateStatsBar(rows) {
    var counts = { active: 0, draft: 0, qc: 0, sold: 0 };

    rows.forEach(function (r) {
        var s = (r.Status || "").toLowerCase();
        if (s === "active") counts.active++;
        else if (s === "draft") counts.draft++;
        else if (s === "qc_scheduled" || s === "qc_passed") counts.qc++;
        else if (s === "sold") counts.sold++;
    });

    $("#statActive").removeClass("skeleton").text(counts.active);
    $("#statDraft").removeClass("skeleton").text(counts.draft);
    $("#statQC").removeClass("skeleton").text(counts.qc);
    $("#statSold").removeClass("skeleton").text(counts.sold);

    if (rows.length > 0) {
        $("#cropsStatsBar").show();
    }
}

// DOCUMENT READY

$(document).ready(function () {

    /* Abort if no token */
    if (!getToken()) {
        redirectToLogin();
        return;
    }

    console.log("Farmer ID:", window.FARMER_ID);

    /* 1. INITIAL LISTINGS LOAD */

    $.ajax({
        url: window.API_BASE + "/" + window.FARMER_ID + "/listings",
        type: "GET",
        headers: authHeaders(),

        success: function (response) {
            var rows = (response.data || []).map(mapListingToRow);

            $("#gridSkeleton").hide();
            updateStatsBar(rows);

            if (rows.length === 0) {
                $("#cropsEmpty").show();
                return;
            }

            var templateHtml = $("#crop-card-template").html();
            if (!templateHtml) {
                console.error("[crops.js] #crop-card-template not found in DOM.");
                return;
            }

            $("#cropsGrid").show().kendoListView({
                dataSource: {
                    data: rows,
                    pageSize: 12
                },
                template: kendo.template(templateHtml)
            });
        },

        error: function (xhr) {
            if (handleUnauthorized(xhr)) return;
            console.error("[crops.js] Failed to load listings:", xhr.status, xhr.responseText);
        }
    });

    /* 2. DROPDOWNS */

    $.ajax({
        url: window.API_BASE + "/dropdowns/catalog",
        type: "GET",
        headers: authHeaders(),

        success: function (res) {
            $("#cropType").kendoDropDownList({
                dataTextField: "name",
                dataValueField: "id",
                dataSource: res.data || [],
                optionLabel: "Select Crop Type..."
            });
        },

        error: function (xhr) {
            if (handleUnauthorized(xhr)) return;
            console.error("[crops.js] Failed to load catalog dropdown");
        }
    });

    /* 3. KENDO WIDGETS */

    $("#cropUnit").kendoDropDownList({
        dataSource: ["kg", "quintal", "ton"]
    });

    $("#cropQty").kendoNumericTextBox({ min: 1, format: "n0" });
    $("#cropPrice").kendoNumericTextBox({ min: 1, format: "n0" });

    $("#harvestDate").kendoDatePicker({
        format: "yyyy-MM-dd",
        max: new Date()
    });

    $("#cropVariety").kendoTextBox();
    $("#farmState").kendoTextBox();
    $("#farmDistrict").kendoTextBox();
    $("#farmLocation").kendoTextArea({ rows: 2, resize: "none" });
    $("#cropNotes").kendoTextArea({ rows: 2, resize: "none" });

    $("#cropWindow").kendoWindow({
        width: "660px",
        title: "List New Crop",
        visible: false,
        modal: true
    });

    /* 4. SEARCH EVENT LISTENERS — THIS WAS THE MISSING PIECE */

    $("#cropSearchInput").on("input", function () {
        clearTimeout(cropSearchTimeout);
        var query = $(this).val().trim();
        cropSearchTimeout = setTimeout(function () {
            elasticSearchCrops(query);
        }, 400);
    });

    $("#cropStatusFilter").on("change", function () {
        var query = $("#cropSearchInput").val().trim();
        if (query) {
            elasticSearchCrops(query);
        }
    });

    $("#clearCropSearchBtn, #clearCropSearch").on("click", function () {
        $("#cropSearchInput").val("");
        $("#searchResultsInfo").hide();
        $("#clearCropSearchBtn").hide();
        reloadListings();
    });

});

// MODAL HELPERS

function openCropWindow() {
    $("#editListingId").val("");
    $("#cropForm")[0].reset();

    var qtyBox = $("#cropQty").data("kendoNumericTextBox");
    if (qtyBox) qtyBox.value(null);

    var priceBox = $("#cropPrice").data("kendoNumericTextBox");
    if (priceBox) priceBox.value(null);

    var dp = $("#harvestDate").data("kendoDatePicker");
    if (dp) dp.value(null);

    $("#cropWindow").data("kendoWindow")
        .title("List New Crop")
        .center()
        .open();
}

function closeCropWindow() {
    $("#cropWindow").data("kendoWindow").close();
}

// SAVE CROP (Create or Update)

function saveCrop(statusMode) {

    var token = getToken();
    if (!token) { redirectToLogin(); return; }

    var editId = $("#editListingId").val();

    var payload = {
        FarmerId: window.FARMER_ID,
        CatalogProductId: $("#cropType").val(),
        Variety: $("#cropVariety").val(),
        AvailableQuantity: $("#cropQty").data("kendoNumericTextBox").value(),
        Unit: $("#cropUnit").data("kendoDropDownList").value(),
        AskingPrice: $("#cropPrice").data("kendoNumericTextBox").value(),
        HarvestDate: kendo.toString(
            $("#harvestDate").data("kendoDatePicker").value(),
            "yyyy-MM-dd"
        ),
        FarmAddress: $("#farmLocation").val(),
        FarmState: $("#farmState").val(),
        FarmDistrict: $("#farmDistrict").val(),
        Notes: $("#cropNotes").val(),
        IsDraft: statusMode === "Draft"
    };

    var isEdit = !!editId;
    var ajaxUrl = isEdit
        ? window.API_BASE + "/listings/" + editId
        : window.API_BASE + "/listings";

    $.ajax({
        url: ajaxUrl,
        type: isEdit ? "PUT" : "POST",
        contentType: "application/json",
        headers: { "Authorization": "Bearer " + token },
        data: JSON.stringify(payload),

        success: function (res) {
            fbSuccess("Saved", res.message || "Listing saved successfully.");
            closeCropWindow();
            reloadListings();
        },

        error: function (xhr) {
            if (xhr.status === 401 || xhr.status === 403) {
                redirectToLogin();
                return;
            }
            fbError("Save Failed", "Failed to save listing. Please try again.");
            console.error("[crops.js] saveCrop error:", xhr.status, xhr.responseText);
        }
    });
}

// EDIT CROP

function editCrop(id) {

    var token = getToken();
    if (!token) { redirectToLogin(); return; }

    $.ajax({
        url: window.API_BASE + "/listings/" + id,
        type: "GET",
        headers: { "Authorization": "Bearer " + token },

        success: function (res) {
            var item = res.data || res;

            $("#editListingId").val(item.listingId);

            var ddCrop = $("#cropType").data("kendoDropDownList");
            if (ddCrop) ddCrop.value(item.catalogProductId);

            $("#cropVariety").val(item.variety || "");

            var qtyBox = $("#cropQty").data("kendoNumericTextBox");
            if (qtyBox) qtyBox.value(item.availableQuantity);

            var ddUnit = $("#cropUnit").data("kendoDropDownList");
            if (ddUnit) ddUnit.value(item.unit);

            var priceBox = $("#cropPrice").data("kendoNumericTextBox");
            if (priceBox) priceBox.value(item.askingPrice);

            var dp = $("#harvestDate").data("kendoDatePicker");
            if (dp && item.harvestDate) dp.value(new Date(item.harvestDate));

            $("#farmLocation").val(item.farmAddress || "");
            $("#farmState").val(item.farmState || "");
            $("#farmDistrict").val(item.farmDistrict || "");
            $("#cropNotes").val(item.notes || "");

            $("#cropWindow").data("kendoWindow")
                .title("Edit Listing #" + id)
                .center()
                .open();
        },

        error: function (xhr) {
            if (handleUnauthorized(xhr)) return;
            console.error("[crops.js] editCrop fetch error:", xhr.status);
        }
    });
}

// DELETE / WITHDRAW CROP

function deleteCrop(id) {

    fbConfirm("Withdraw Listing?", "This will remove the listing from the marketplace.", "Yes, Withdraw").then(function (confirmed) {
        if (!confirmed) return;

        var token = getToken();
        if (!token) { redirectToLogin(); return; }

        $.ajax({
            url: window.API_BASE + "/" + window.FARMER_ID + "/crop/" + id,
            type: "DELETE",
            headers: { "Authorization": "Bearer " + token },

            success: function (res) {
                fbSuccess("Withdrawn", res.message || "Listing removed successfully.");
                reloadListings();
            },

            error: function (xhr) {
                if (handleUnauthorized(xhr)) return;
                fbError("Error", "Could not withdraw listing.");
                console.error("[crops.js] delete error:", xhr.status, xhr.responseText);
            }
        });
    });
}

// VIEW HISTORY

function viewHistory(id) {
    window.location.href = "/Farmer/Listings/" + id;
}

// RELOAD LISTINGS

function reloadListings() {

    var token = getToken();
    if (!token) { redirectToLogin(); return; }

    $.ajax({
        url: window.API_BASE + "/" + window.FARMER_ID + "/listings",
        type: "GET",
        headers: { "Authorization": "Bearer " + token },

        success: function (response) {
            var rows = (response.data || []).map(mapListingToRow);

            $("#gridSkeleton").hide();
            updateStatsBar(rows);

            if (rows.length === 0) {
                $("#cropsEmpty").show();
                var lv = $("#cropsGrid").data("kendoListView");
                if (lv) lv.dataSource.data([]);
                return;
            }

            $("#cropsEmpty").hide();

            var lv = $("#cropsGrid").show().data("kendoListView");
            if (lv) {
                lv.dataSource.data(rows);
            }
        },

        error: function (xhr) {
            if (handleUnauthorized(xhr)) return;
            console.error("[crops.js] reloadListings error:", xhr.status);
        }
    });
}

// ========== ELASTICSEARCH SEARCH ==========

let cropSearchTimeout;

async function elasticSearchCrops(query) {
    console.log("Searching for:", query);

    if (!query || query.trim() === "") {
        document.getElementById("searchResultsInfo").style.display = "none";
        const clearBtn = document.getElementById("clearCropSearchBtn");
        if (clearBtn) clearBtn.style.display = "none";
        reloadListings();
        return;
    }

    const clearBtn = document.getElementById("clearCropSearchBtn");
    if (clearBtn) clearBtn.style.display = "inline-flex";

    document.getElementById("searchResultsInfo").style.display = "block";
    document.getElementById("searchQueryText").innerText = query;

    try {
        const status = document.getElementById("cropStatusFilter").value;

        const response = await fetch("/Farmer/SearchMyCrops", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
                Query: query,
                Page: 1,
                PageSize: 50,
                Status: status || null
            })
        });

        if (!response.ok) {
            console.error("Search failed with status:", response.status);
            displayNoResults();
            return;
        }

        const text = await response.text();

        if (!text || text.trim() === "") {
            displayNoResults();
            return;
        }

        let result;
        try {
            result = JSON.parse(text);
        } catch (e) {
            console.error("Failed to parse JSON:", e.message);
            displayNoResults();
            return;
        }

        console.log("Search results:", result);

        if (result && result.results && result.results.length > 0) {
            displaySearchResults(result.results);
        } else {
            displayNoResults();
        }

    } catch (error) {
        console.error("Search error:", error);
        displayNoResults();
    }
}

function displaySearchResults(results) {
    const skeleton = document.getElementById("gridSkeleton");
    if (skeleton) skeleton.style.display = "none";

    const empty = document.getElementById("cropsEmpty");
    if (empty) empty.style.display = "none";

    const rows = results.map(r => ({
        Id:           r.id                 || 0,
        Crop:         r.cropName           || "—",
        Variety:      r.variety            || "Standard",
        Qty:          r.quantityAvailable  || 0,
        Unit:         r.unit               || "kg",
        Price:        r.askingPrice        || 0,
        HarvestDate:  "—",
        Status:       r.status             || "draft",
        FarmDistrict: r.farmDistrict       || "—",
        FarmState:    r.farmState          || "—",
        Notes:        r.notes              || ""
    }));

    const templateHtml = document.getElementById("crop-card-template").innerHTML;
    if (!templateHtml) return;

    const lv = $("#cropsGrid").show().data("kendoListView");
    if (lv) {
        lv.dataSource.data(rows);
    } else {
        $("#cropsGrid").show().kendoListView({
            dataSource: { data: rows, pageSize: 12 },
            template: kendo.template(templateHtml)
        });
    }
}

function displayNoResults() {
    $("#gridSkeleton").hide();
    $("#cropsEmpty").show();          // already exists in cshtml

    var lv = $("#cropsGrid").data("kendoListView");
    if (lv) {
        lv.dataSource.data([]);       // widget safe rehta hai
    }
    $("#cropsGrid").hide();
}

function displaySearchResults(results) {
    $("#gridSkeleton").hide();
    $("#cropsEmpty").hide();

    var rows = results.map(function(r) {
        return {
            Id:           r.id                || 0,
            Crop:         r.cropName          || "—",
            Variety:      r.variety           || "Standard",
            Qty:          r.quantityAvailable || 0,
            Unit:         r.unit              || "kg",
            Price:        r.askingPrice       || 0,
            HarvestDate:  r.harvestDate ? kendo.toString(new Date(r.harvestDate), "dd MMM yyyy") : "—", 
            Status:       r.status            || "draft",
            FarmDistrict: r.farmDistrict      || "—",
            FarmState:    r.farmState         || "—",
            Notes:        r.notes             || ""
        };
    });

    var lv = $("#cropsGrid").show().data("kendoListView");
    if (lv) {
        lv.dataSource.data(rows);     // widget intact rehta hai
    }
}