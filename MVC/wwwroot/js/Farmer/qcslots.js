/**
 * Farmer QC Slots — qcslots.js
 * JWT Bearer token is read from cookie: "authToken"
 * Multi-farmer support enabled
 */

$(document).ready(function () {

    // CONFIG

    window.API_BASE = "http://localhost:5020/api/FarmerApp";


    // AUTH HELPERS

    function getToken() {
        var match = document.cookie.match(/(?:^|;\s*)authToken=([^;]+)/);
        return match ? decodeURIComponent(match[1]) : null;
    }


    /* Extract FarmerId from JWT */

    function getFarmerId() {

        try {

            var token = getToken();

            if (!token) return null;

            var payload = JSON.parse(
                atob(token.split('.')[1])
            );

            console.log("JWT Payload:", payload);

            return (
                payload.farmer_id ||
                payload.farmerId ||
                payload.FarmerId ||
                payload["farmer_id"]
            );

        }
        catch (e) {

            console.error("JWT Parse Error:", e);

            return null;
        }
    }

    window.FARMER_ID = getFarmerId();

    console.log("Token:", getToken());
    console.log("Farmer ID:", window.FARMER_ID);
    function authHeaders() {

        var token = getToken();

        if (!token) {
            redirectToLogin();
            return {};
        }

        return {
            "Authorization": "Bearer " + token
        };
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


    function showLoadError(section) {
        console.error("Failed to load:", section);
    }


    /* Abort if missing */

    if (!getToken() || !window.FARMER_ID) {

        console.error("Missing Token or Farmer ID");

        redirectToLogin();

        return;
    }


    console.log("API Base:", window.API_BASE);
    console.log("Farmer ID:", window.FARMER_ID);


    // WINDOW INIT

    $("#qcBookWindow").kendoWindow({
        width: "550px",
        title: "Request QC Appointment",
        visible: false,
        modal: true
    });


    // 1. QC DASHBOARD

    $.ajax({

        url: `${window.API_BASE}/${window.FARMER_ID}/qc-dashboard`,

        type: "GET",

        headers: authHeaders(),

        success: function (res) {

            if (res.success) {

                var d = res.data;

                $("#kpiUpcoming").text(d.upcomingAppts);
                $("#kpiAwaiting").text(d.awaitingResults);
                $("#kpiTotalPassed").text(d.totalQcPassed);
                $("#kpiGradeRate").text(d.premiumGradeRate + "%");


                $("#historyGrid").kendoGrid({

                    dataSource: {

                        data: (d.appointments || []).map(function (item) {

                            return {

                                Date: kendo.toString(
                                    new Date(item.slotDate),
                                    "dd MMM yyyy"
                                ),

                                Time: item.timeStart,

                                Crop: item.cropName,

                                Status: item.status
                            };

                        }),

                        pageSize: 5
                    },

                    pageable: true,

                    columns: [

                        { field: "Date", title: "Date", width: "100px" },

                        { field: "Time", title: "Time", width: "90px" },

                        { field: "Crop", title: "Crop" },

                        {
                            field: "Status",
                            title: "Status",
                            template:
                                "<span class='status-up'>#:Status#</span>"
                        }

                    ]

                });

            }

        },

        error: function (xhr) {

            if (handleUnauthorized(xhr)) return;

            showLoadError("QC dashboard");

        }

    });


    // 2. WAREHOUSE DROPDOWN
    $.ajax({
        url: `${window.API_BASE}/dropdowns/warehouses`,
        type: "GET",
        headers: authHeaders(),
        success: function (res) {
            $("#warehouseLocation").kendoDropDownList({
                dataTextField: "name",
                dataValueField: "id",
                dataSource: res.data || [],
                optionLabel: "Select Nearest Warehouse...",

                // ADD THIS NEW CHANGE EVENT:
                change: function () {
                    var datePicker = $("#slotDate").data("kendoDatePicker");
                    var selectedDate = datePicker ? datePicker.value() : null;

                    // If they already picked a date, load the slots for this new warehouse!
                    if (selectedDate && this.value()) {
                        fetchTimeSlots(this.value());
                    } else {
                        $("#slotsContainer").hide();
                        $("#confirmBookingBtn").prop("disabled", true);
                    }
                }
            });
        },
        error: function (xhr) {
            if (handleUnauthorized(xhr)) return;
            showLoadError("warehouse dropdown");
        }
    });

    // 3. CROP LISTINGS DROPDOWN

    $.ajax({

        url: `${window.API_BASE}/${window.FARMER_ID}/dropdowns/active-listings`,

        type: "GET",

        headers: authHeaders(),

        success: function (res) {

            $("#cropListing").kendoDropDownList({

                dataTextField: "name",

                dataValueField: "id",

                dataSource: res.data || [],

                optionLabel: "Select Crop for Inspection..."

            });

        },

        error: function (xhr) {

            if (handleUnauthorized(xhr)) return;

            showLoadError("crop listing dropdown");

        }

    });


    $("#slotDate").kendoDatePicker({

        // This blocks all future dates by setting the maximum date to right now!
        min: new Date(),

        format: "yyyy-MM-dd",

        change: function () {

            var warehouseId = $("#warehouseLocation").val();

            if (this.value() && warehouseId)
                fetchTimeSlots(warehouseId);
            else
                $("#slotsContainer").hide();

        }

    });

    // CONFIRM BOOKING

    $(document).on("click", "#confirmBookingBtn", function () {

        var payload = {

            FarmerId: window.FARMER_ID,

            WarehouseId: $("#warehouseLocation").val(),

            CropListingId: $("#cropListing").val(),

            SlotId: window.SELECTED_SLOT_ID,

            SlotDate: kendo.toString(
                $("#slotDate")
                    .data("kendoDatePicker")
                    .value(),
                "yyyy-MM-dd"
            ),

            TimeStart: window.SELECTED_SLOT_START,

            TimeEnd: window.SELECTED_SLOT_END
        };


        $.ajax({

            url: `${window.API_BASE}/slots/book`,

            type: "POST",

            contentType: "application/json",

            headers: authHeaders(),

            data: JSON.stringify(payload),

            success: function (res) {

                Swal.fire({

                    title: "Confirmed!",

                    text: res.message,

                    icon: "success",

                    confirmButtonColor: "#10b981"

                });

                closeQCWindow();

                setTimeout(function () {
                    location.reload();
                }, 1500);

            },

            error: function (xhr) {

                if (handleUnauthorized(xhr)) return;

                showLoadError("booking confirmation");

            }

        });

    });

    $("#btnOpenQCWindow").on("click", openQCSlotWindow);
    $("#btnCloseQCWindow").on("click", closeQCWindow);

});


// WINDOW HELPERS

function openQCSlotWindow() {

    $("#qcBookForm")[0].reset();

    $("#slotsContainer").hide();

    $("#confirmBookingBtn").prop("disabled", true);

    $("#qcBookWindow")
        .data("kendoWindow")
        .center()
        .open();
}


function closeQCWindow() {

    $("#qcBookWindow")
        .data("kendoWindow")
        .close();
}


// FETCH TIME SLOTS
function fetchTimeSlots(warehouseId) {
    // 1. Get the exact date the user picked as a string (e.g., "2026-04-17")
    var selectedDateStr = kendo.toString($("#slotDate").data("kendoDatePicker").value(), "yyyy-MM-dd");

    $.ajax({
        url: `${window.API_BASE}/warehouses/${warehouseId}/slots`,
        type: "GET",
        headers: {
            // Safely grab the cookie
            "Authorization": "Bearer " + (document.cookie.match(/(?:^|;\s*)authToken=([^;]+)/) ? decodeURIComponent(document.cookie.match(/(?:^|;\s*)authToken=([^;]+)/)[1]) : "")
        },
        success: function (res) {
            $("#slotsContainer").show();
            var grid = $("#timeSlotGrid").empty();
            var foundSlots = false;

            // Safely handle if C# returns data or Data
            var slotsArray = res.data || res.Data || [];

            slotsArray.forEach(function (slot) {
                // 2. Extract values safely handling both camelCase and PascalCase from C#
                var rawDate = slot.slotDate || slot.SlotDate;
                var tStart = slot.timeStart || slot.TimeStart;
                var tEnd = slot.timeEnd || slot.TimeEnd;
                var cap = slot.availableCapacityMt || slot.AvailableCapacityMt;
                var sId = slot.slotId || slot.SlotId;

                if (!rawDate) return;

                // 3. Safely extract YYYY-MM-DD
                var slotDateOnly = rawDate.split('T')[0];

                // 4. ONLY show if the date exactly matches the calendar
                if (slotDateOnly === selectedDateStr) {
                    foundSlots = true;

                    // Clean up times from "09:00:00" to "09:00"
                    var displayStart = tStart.substring(0, 5);
                    var displayEnd = tEnd.substring(0, 5);

                    var btn = $("<button type='button' class='slot-btn available'>" +
                        displayStart + " - " + displayEnd +
                        "<div class='slot-capacity'>Available (" + cap + " MT)</div></button>");

                    btn.click(function () {
                        $(".slot-btn").removeClass("selected");
                        $(this).addClass("selected");
                        window.SELECTED_SLOT_ID = sId;
                        window.SELECTED_SLOT_START = tStart;
                        window.SELECTED_SLOT_END = tEnd;
                        $("#confirmBookingBtn").prop("disabled", false);
                    });

                    grid.append(btn);
                }
            });

            // 5. Show friendly message if no slots match the database
            if (!foundSlots) {
                grid.append("<div style='grid-column: 1/-1; text-align: center; color: #64748b; font-size: 13px; padding: 10px; font-weight: 500;'>No slots available for this specific date. Please select another day.</div>");
            }
        },
        error: function (xhr) {
            if (xhr.status === 401 || xhr.status === 403) {
                window.location.href = "/Farmer/Login";
            } else {
                console.error("Failed to load time slots", xhr);
            }
        }
    });
}