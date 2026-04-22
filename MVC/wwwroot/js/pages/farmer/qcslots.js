$(document).ready(function () {

    $("#qcBookWindow").kendoWindow({
        width: "550px",
        title: "Request QC Appointment",
        visible: false,
        modal: true,
        actions: ["Close"]
    });

    $("#slotDate").kendoDatePicker({
        min: new Date(),
        value: null,
        format: "yyyy-MM-dd",
        change: function () {
            if (this.value()) fetchTimeSlots(this.value());
            else $("#slotsContainer").hide();
        }
    });

    $("#historyGrid").kendoGrid({
        dataSource: {
            pageSize: 5
        },
        pageable: true,
        autoBind: false,
        noRecords: { template: "<div style='padding:20px; text-align:center; color:#94a3b8;'>No slot history found.</div>" },
        columns: [
            { field: "Date",  title: "Date",  width: "100px" },
            { field: "Time",  title: "Time",  width: "90px" },
            { field: "Crop",  title: "Crop" },
            {
                field: "Status", title: "Status",
                template: function (d) {
                    var cls = d.Status === "Upcoming" ? "status-up" : (d.Status === "Completed" ? "status-comp" : "status-canc");
                    return "<span class='" + cls + "'>" + d.Status + "</span>";
                }
            },
            {
                title: "Action", width: 140,
                template: function (d) {
                    if (d.Status === "Upcoming") {
                        return "<button class='k-button k-button-sm k-button-solid k-button-solid-base' onclick='editSlot(" + d.Id + ")' style='margin-right:4px;'><i class='fi fi-rr-edit'></i> Edit</button>"
                             + "<button class='k-button k-button-sm k-button-solid k-button-solid-error' onclick='deleteSlot(" + d.Id + ")'><i class='fi fi-rr-trash'></i> Cancel</button>";
                    }
                    return "";
                }
            }
        ]
    });

    // Bind confirm button inside ready so Kendo Window element is available
    $(document).on("click", "#confirmBookingBtn", function () {
        var selectedTime = $(".slot-btn.selected").contents().filter(function () {
            return this.nodeType === 3;
        }).text().trim();
        var date = kendo.toString($("#slotDate").data("kendoDatePicker").value(), "yyyy-MM-dd");

        fbConfirm(
            "Confirm Appointment?",
            "Schedule QC inspection for " + date + " at " + selectedTime + "?",
            "Yes, Book It!"
        ).then(function (confirmed) {
            if (!confirmed) return;
            fbSuccess("Booking Confirmed", "Your appointment is scheduled. The Field Officer will meet you at the warehouse.");
            closeQCWindow();
        });
    });
});

function openQCSlotWindow() {
    $("#qcBookForm")[0].reset();
    $("#slotsContainer").hide();
    $("#confirmBookingBtn").prop("disabled", true);
    $("#warehouseLocation").val("");
    $("#cropListing").val("");
    $("#slotDate").data("kendoDatePicker").value(null);
    $("#qcBookWindow").data("kendoWindow").center().open();
}

function closeQCWindow() {
    $("#qcBookWindow").data("kendoWindow").close();
}

// Expected API response: [{ TimeStr: "09:00 AM", IsAvailable: true }, ...]
function fetchTimeSlots(selectedDate) {
    var dateString = kendo.toString(selectedDate, "yyyy-MM-dd");
    $.ajax({
        url: "/api/qcslots/available",
        method: "GET",
        data: { date: dateString },
        success: function (response) { renderSlots(response); },
        error: function () {
            fbError("Could not Load Time Slots", "Please check the API connection and try again.");
        }
    });
}

function renderSlots(slots) {
    $("#slotsContainer").show();
    var grid = $("#timeSlotGrid").empty();

    slots.forEach(function (slot) {
        var btn;
        if (!slot.IsAvailable) {
            btn = $("<button type='button' disabled class='slot-btn booked'"
                  + " style='padding:10px; border:1px solid #e2e8f0; background:#f8fafc; border-radius:8px;"
                  + " font-size:13px; font-weight:500; color:#94a3b8; cursor:not-allowed; opacity:0.6; text-decoration:line-through;'>"
                  + slot.TimeStr + "<div style='font-size:10px; margin-top:2px; font-weight:400; text-decoration:none;'>Booked</div></button>");
        } else {
            btn = $("<button type='button' class='slot-btn available'"
                  + " style='padding:10px; border:1px solid #cbd5e0; background:#fff; border-radius:8px;"
                  + " font-size:13px; font-weight:600; color:#4a5568; cursor:pointer; transition:all 0.2s;'>"
                  + slot.TimeStr + "<div style='font-size:10px; margin-top:2px; font-weight:400; color:#10b981;'>Available</div></button>");
            btn.click(function () {
                $(".slot-btn.available").css({ background: "#fff", color: "#4a5568", "border-color": "#cbd5e0", "box-shadow": "none" }).removeClass("selected");
                $(this).css({ background: "#ecfdf5", color: "#047857", "border-color": "#10b981", "box-shadow": "0 0 0 3px rgba(16,185,129,0.15)" }).addClass("selected");
                $("#confirmBookingBtn").prop("disabled", false);
            });
        }
        grid.append(btn);
    });
}

function editSlot(id) {
    openQCSlotWindow();
    fbAlert("Loading appointment #" + id + "...", "Loading");
}

function deleteSlot(id) {
    fbConfirm("Cancel Appointment?", "Cancellations must be made at least 2 hours before the scheduled time.", "Yes, Cancel").then(function (confirmed) {
        if (!confirmed) return;
        // TODO: DELETE /api/qcslots/:id
        fbSuccess("Cancelled", "Slot removed. Capacity has been released.");
    });
}
