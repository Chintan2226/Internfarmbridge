/**
 * Farmer Crops — crops.js
 * JWT Bearer token is read from cookie: "authToken"
 * Multi-farmer support enabled
 *
 * FIX LOG:
 *  1. FarmDistrict, FarmState, Notes added to rows map in BOTH load & reload
 *  2. getToken() defined once at module scope — no more duplicates
 *  3. Template uses #: (HTML-encode) not #= to avoid Razor hash issues
 *  4. Null-check on template element before kendoListView init
 *  5. saveCrop payload now includes FarmState, FarmDistrict, Notes
 *  6. Stats bar updated after every load
 *  7. FIX: HTML entities (&#8377;, &#9998;) removed from cshtml template —
 *     use literal Unicode characters (₹, ✎) instead. The browser decodes
 *     HTML entities before Kendo parses the template string, injecting raw
 *     & characters that break kendo.template()'s code generator.
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

        // FIX: these were missing and caused the ReferenceError
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

    $("#statActive").text(counts.active);
    $("#statDraft").text(counts.draft);
    $("#statQC").text(counts.qc);
    $("#statSold").text(counts.sold);

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

    /* ── 1. INITIAL LISTINGS LOAD ────────────────── */

    $.ajax({
        url: window.API_BASE + "/" + window.FARMER_ID + "/listings",
        type: "GET",
        headers: authHeaders(),

        success: function (response) {
            var rows = (response.data || []).map(mapListingToRow);

            if (rows.length === 0) {
                $("#cropsEmpty").show();
                return;
            }

            /* ✅ FIX: guard against missing template element */
            var templateHtml = $("#crop-card-template").html();
            if (!templateHtml) {
                console.error("[crops.js] #crop-card-template not found in DOM.");
                return;
            }

            $("#cropsGrid").kendoListView({
                dataSource: {
                    data: rows,
                    pageSize: 12
                },
                template: kendo.template(templateHtml)
            });

            updateStatsBar(rows);
        },

        error: function (xhr) {
            if (handleUnauthorized(xhr)) return;
            console.error("[crops.js] Failed to load listings:", xhr.status, xhr.responseText);
        }
    });

    /* ── 2. DROPDOWNS ────────────────────────────── */

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

    /* ── 3. KENDO WIDGETS ────────────────────────── */

    $("#cropUnit").kendoDropDownList({
        dataSource: ["kg", "quintal", "ton"]
    });

    $("#cropQty").kendoNumericTextBox({
        min: 1,
        format: "n0"
    });

    $("#cropPrice").kendoNumericTextBox({
        min: 1,
        format: "n0"
    });

    $("#harvestDate").kendoDatePicker({
        format: "yyyy-MM-dd",
        max: new Date()
    });
    
    /* Additional Premium Kendo Inputs */
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

});

// MODAL HELPERS

function openCropWindow() {
    $("#editListingId").val("");
    $("#cropForm")[0].reset();

    // Reset Kendo widgets that don't reset with the form
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

    // FIX: payload now includes FarmState, FarmDistrict, Notes
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
            Swal.fire({
                icon: "success",
                title: "Success",
                text: res.message || "Listing saved successfully."
            });
            closeCropWindow();
            reloadListings();
        },

        error: function (xhr) {
            if (xhr.status === 401 || xhr.status === 403) {
                redirectToLogin();
                return;
            }
            Swal.fire({
                icon: "error",
                title: "Error",
                text: "Failed to save listing. Please try again."
            });
            console.error("[crops.js] saveCrop error:", xhr.status, xhr.responseText);
        }
    });
}

// EDIT CROP — load existing data into modal

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

            // Populate Kendo widgets
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

    Swal.fire({
        icon: "warning",
        title: "Withdraw Listing?",
        text: "This will remove the listing from the marketplace.",
        showCancelButton: true,
        confirmButtonText: "Yes, Withdraw",
        confirmButtonColor: "#ef4444"
    }).then(function (result) {

        if (!result.isConfirmed) return;

        var token = getToken();
        if (!token) { redirectToLogin(); return; }

        $.ajax({
            url: window.API_BASE + "/" + window.FARMER_ID + "/crop/" + id,
            type: "DELETE",
            headers: { "Authorization": "Bearer " + token },

            success: function (res) {
                Swal.fire({
                    icon: "success",
                    title: "Withdrawn",
                    text: res.message || "Listing removed successfully."
                });

                reloadListings();
            },

            error: function (xhr) {
                if (handleUnauthorized(xhr)) return;

                Swal.fire({
                    icon: "error",
                    title: "Error",
                    text: "Could not withdraw listing."
                });

                console.error("[crops.js] delete error:", xhr.status, xhr.responseText);
            }
        });

    });
}

// VIEW HISTORY

function viewHistory(id) {
    window.location.href = "/Farmer/Listings/" + id;
}

// RELOAD LISTINGS — refreshes grid in-place

function reloadListings() {

    var token = getToken();
    if (!token) { redirectToLogin(); return; }

    $.ajax({
        url: window.API_BASE + "/" + window.FARMER_ID + "/listings",
        type: "GET",
        headers: { "Authorization": "Bearer " + token },

        success: function (response) {
            var rows = (response.data || []).map(mapListingToRow);

            if (rows.length === 0) {
                $("#cropsEmpty").show();
                var lv = $("#cropsGrid").data("kendoListView");
                if (lv) lv.dataSource.data([]);
                return;
            }

            $("#cropsEmpty").hide();

            var lv = $("#cropsGrid").data("kendoListView");
            if (lv) {
                lv.dataSource.data(rows);
            }

            updateStatsBar(rows);
        },

        error: function (xhr) {
            if (handleUnauthorized(xhr)) return;
            console.error("[crops.js] reloadListings error:", xhr.status);
        }
    });
}