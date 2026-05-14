let listView, dataSource;
let editMode = false;

$(function () {
    dataSource = new kendo.data.DataSource({
        transport: {
            read: function(options) {
                // 👉 Show Skeleton
                $("#catalogListView").hide();
                if ($("#catalogSkeleton").length === 0) {
                    $("<div id='catalogSkeleton' class='k-listview-content'></div>").insertBefore("#catalogListView");
                }
                if (typeof FBSkeleton !== 'undefined') {
                    FBSkeleton.show('#catalogSkeleton', 8, 'crop');
                } else {
                    $("#catalogSkeleton").html("<div style='text-align:center;padding:20px;'>Loading crops...</div>");
                }
                $("#catalogSkeleton").show();

                // Call local MVC endpoint (which proxies to API) or call API directly?
                // The existing logic used /Admin/GetAll which is a proxy in AdminController.cs
                // We'll keep the proxy calls for read/add/edit/delete as they might have logic there,
                // but we'll ensure they are reliable.
                $.ajax({
                    url: "/Admin/GetAll",
                    dataType: "json",
                    success: function(result) {
                        $("#catalogSkeleton").remove();
                        $("#catalogListView").show();
                        
                        const actualData = Array.isArray(result) ? result : (result.data || []);
                        options.success(actualData);
                    },
                    error: function(err) {
                        $("#catalogSkeleton").remove();
                        $("#catalogListView").show();
                        options.error(err);
                    }
                });
            }
        },
        schema: {
            model: {
                fields: {
                    id:            { type: "number"  },
                    name:          { type: "string"  },
                    category:      { type: "string"  },
                    unitOfMeasure: { type: "string"  },
                    imageUrl:      { type: "string"  },
                    isActive:      { type: "boolean" },
                    createdByName: { type: "string"  },
                    createdAt:     { type: "date"    }
                }
            }
        },
        pageSize: 12,
        serverPaging: false,
        serverFiltering: false,
        serverSorting: false
    });

    listView = $("#catalogListView").kendoListView({
        dataSource: dataSource,
        template:      kendo.template($("#cardTemplate").html()),
        altTemplate:   kendo.template($("#cardTemplate").html()),
        noRecords:     { template: kendo.template($("#emptyTemplate").html()) }
    }).data("kendoListView");

    $("#catalogPager").kendoPager({
        dataSource: dataSource,
        input: true,
        numeric: true,
        previousNext: true,
        info: true,
        messages: { display: "Showing {0}-{1} of {2} crops", empty: "No crops to display" }
    });

    $("#filterCategory").kendoDropDownList({
        dataSource: ["", "Grain", "Vegetables", "Fruits", "Pulses", "Spices", "Oilseeds"],
        optionLabel: "All Categories",
        change: applyFilters
    });
    $("#filterStatus").kendoDropDownList({
        dataSource: [
            { text: "All Status", value: "" },
            { text: "Active",     value: "active"   },
            { text: "Inactive",   value: "inactive" }
        ],
        dataTextField:  "text",
        dataValueField: "value",
        change: applyFilters
    });

    $("#filterSearch").on("keyup", applyFilters);
});

function applyFilters() {
    const search = ($("#filterSearch").val() || "").toLowerCase();
    const cat    = $("#filterCategory").data("kendoDropDownList").value();
    const status = $("#filterStatus").data("kendoDropDownList").value();

    const filters = [];

    if (search) {
        filters.push({
            logic: "or",
            filters: [
                { field: "name",     operator: "contains", value: search },
                { field: "category", operator: "contains", value: search }
            ]
        });
    }
    if (cat)    filters.push({ field: "category", operator: "eq", value: cat });
    if (status === "active")   filters.push({ field: "isActive", operator: "eq", value: true  });
    if (status === "inactive") filters.push({ field: "isActive", operator: "eq", value: false });

    dataSource.filter(filters.length ? { logic: "and", filters } : {});
}

function clearFilters() {
    $("#filterSearch").val("");
    $("#filterCategory").data("kendoDropDownList").value("");
    $("#filterStatus").data("kendoDropDownList").value("");
    dataSource.filter({});
}

function previewImage(input) {
    if (!input.files?.[0]) return;
    if (input.files[0].size > 5 * 1024 * 1024) { Swal.fire({title:"Image must be under 5 MB.",icon:"warning"}); input.value = ""; return; }
    const r = new FileReader();
    r.onload = e => { $("#imgPreview").attr("src", e.target.result).show(); $("#imgPlaceholder").hide(); $("#imgRemoveBtn").show(); };
    r.readAsDataURL(input.files[0]);
}
function removeImage(e) {
    e.stopPropagation();
    $("#fImageFile").val("");
    $("#imgPreview").attr("src", "").hide();
    $("#imgPlaceholder").show();
    $("#imgRemoveBtn").hide();
    $("#fExistingImageUrl").val("");
}

function openAddModal()  { editMode = false; resetForm(); openDialog("Add Crop Type"); }

function openEditModal(id) {
    editMode = true; resetForm();
    $.getJSON(`/Admin/GetById/${id}`, p => {
        $("#fId").val(p.id);
        $("#fName").val(p.name);
        $("#fCategory").val(p.category || "");
        $("#fUnit").val(p.unitOfMeasure);
        $("#fDescription").val(p.description || "");
        $("#fIsActive").prop("checked", p.isActive);
        $("#fExistingImageUrl").val(p.imageUrl || "");
        if (p.imageUrl) {
            $("#imgPreview").attr("src", p.imageUrl).show();
            $("#imgPlaceholder").hide();
            $("#imgRemoveBtn").show();
        }
        if (p.qualityParameters) {
            let qp = p.qualityParameters;
            if (typeof qp === "string") { try { qp = JSON.parse(qp); } catch { qp = {}; } }
            $("#qpGrade").val(qp.grade || "");
            $("#qpSize").val(qp.size || "");
            $("#qpMoisture").val(qp.moisture || "");
            $("#qpPurity").val(qp.purity || "");
            $("#qpColor").val(qp.color || "");
            $("#qpVariety").val(qp.variety || "");
        }
    });
    openDialog("Edit Crop Type");
}

function openDialog(title) {
    const prev = $("#productDialog").data("kendoDialog");
    if (prev) { prev.destroy(); $("#productDialog").remove(); }

    $("body").append(`<div id="productDialog">${$("#productDialogTemplate").html()}</div>`);

    $("#productDialog").kendoDialog({
        width:    "600px",
        height:   "auto",
        maxHeight: "88vh",
        title,
        modal: true,
        closable: true,
        actions: [
            { text: "Cancel" },
            {
                text:    editMode ? "💾 Save Changes" : "✅ Add Crop Type",
                primary: true,
                action() { saveProduct(); return false; }
            }
        ],
        open() {
            $(".k-dialog-content").css({ "max-height": "62vh", "overflow-y": "auto" });
        }
    }).data("kendoDialog").open();
}

function resetForm() {
    $("#fId,#fName,#fCategory,#fUnit,#fDescription,#fExistingImageUrl").val("");
    $("#fIsActive").prop("checked", true);
    $("#fImageFile").val("");
    $("#imgPreview").attr("src", "").hide();
    $("#imgPlaceholder").show();
    $("#imgRemoveBtn").hide();
    $("#depWarning").hide();
    $("#qpGrade,#qpSize,#qpMoisture,#qpPurity,#qpColor,#qpVariety").val("");
}

function saveProduct() {
    const name = $("#fName").val().trim();
    const unit = $("#fUnit").val().trim();
    if (!name) { Swal.fire({title:"Product name is required.",icon:"warning"}); return; }
    if (!unit) { Swal.fire({title:"Unit of measure is required.",icon:"warning"}); return; }

    let qpObj = {
        grade: $("#qpGrade").val(), size: $("#qpSize").val(),
        moisture: $("#qpMoisture").val(), purity: $("#qpPurity").val(),
        color: $("#qpColor").val(), variety: $("#qpVariety").val()
    };
    Object.keys(qpObj).forEach(k => { if (!qpObj[k]) delete qpObj[k]; });

    const fd = new FormData();
    fd.append("Id",               $("#fId").val() || "0");
    fd.append("Name",             name);
    fd.append("Category",         $("#fCategory").val().trim());
    fd.append("UnitOfMeasure",    unit);
    fd.append("Description",      $("#fDescription").val().trim());
    fd.append("QualityParameters", JSON.stringify(qpObj));
    fd.append("IsActive",         $("#fIsActive").prop("checked").toString());
    fd.append("ExistingImageUrl", $("#fExistingImageUrl").val());
    const img = $("#fImageFile")[0].files[0];
    if (img) fd.append("ImageFile", img);

    $.ajax({
        url: editMode ? "/Admin/Edit" : "/Admin/Add",
        type: "POST", data: fd, processData: false, contentType: false,
        success(res) {
            if (res.success) {
                $("#productDialog").data("kendoDialog")?.close();
                dataSource.read();
                Swal.fire({title:res.message,icon:"success"});
            } else {
                Swal.fire({title:res.message || "Operation failed.",icon:"error"});
            }
        },
        error(xhr) { Swal.fire({title:"Server error.",icon:"error"}); }
    });
}

function imgFallback(el) {
    el.parentElement.innerHTML = '<div class="crop-card-img-placeholder">🌾</div>';
}

function confirmDelete(id) {
    const item = dataSource.data().find(function(d) { return d.id === id; });
    const name = item ? item.name : "this crop";
    $("<div>")
        .html("<p>Delete <strong>" + kendo.htmlEncode(name) + "</strong>?</p>")
        .kendoDialog({
            title: "Confirm Delete", modal: true, width: "390px", closable: true,
            actions: [
                { text: "Cancel" },
                { text: "🗑️ Delete", primary: true, action: function() { deleteProduct(id); return true; } }
            ]
        }).data("kendoDialog").open();
}

function deleteProduct(id) {
    $.ajax({
        url: `/Admin/Delete/${id}`, type: "POST",
        success(res) {
            if (res.success) { dataSource.read(); Swal.fire({title:res.message,icon:"success"}); }
            else              { Swal.fire({title:res.message,icon:"error"}); }
        },
        error(xhr) { Swal.fire({title:"Delete failed.",icon:"error"}); }
    });
}

async function toggleStatus(id, makeActive) {
    const item    = dataSource.data().find(d => d.id === id);
    const name    = item ? item.name : "this crop";
    const action  = makeActive ? "Activate" : "Deactivate";
    const emoji   = makeActive ? "▶" : "⏸";

    const API_BASE = window.API_BASE || 'http://localhost:5020/api/Admin';

    // Confirm before toggling
    $("<div>")
        .html(`<p>${emoji} <strong>${action}</strong> <strong>${kendo.htmlEncode(name)}</strong>?</p>`)
        .kendoDialog({
            title:    `${action} Crop`,
            modal:    true,
            width:    "360px",
            closable: true,
            actions: [
                { text: "Cancel" },
                {
                    text:    `${emoji} ${action}`,
                    primary: true,
                    action: async function () {
                        try {
                            const res = await authFetch(`${API_BASE}/ToggleStatus`, {
                                method: "PUT",
                                body: JSON.stringify({ id: id, isActive: makeActive })
                            });
                            
                            const data = await res.json();
                            if (res.ok) {
                                dataSource.read();
                                showToast(data.message || "Status updated", "success");
                            } else {
                                showToast(data.message || "Status update failed.", "error");
                            }
                        } catch (err) {
                            showToast("Connection error.", "error");
                        }
                        return true;   // close dialog
                    }
                }
            ]
        }).data("kendoDialog").open();
}

function showToast(message, type) {
    if ($.fn.kendoNotification) {
        const el = $("<span>").appendTo("body");
        el.kendoNotification({
            position:   { pinned: true, top: 20, right: 20 },
            autoHideAfter: 3000,
            stacking:   "down",
            templates:  [
                { type: "success", template: "<div class='k-notification-wrap'><span class='k-icon k-i-check'></span>#= message #</div>" },
                { type: "error",   template: "<div class='k-notification-wrap'><span class='k-icon k-i-warning'></span>#= message #</div>" }
            ]
        }).data("kendoNotification").show({ message }, type);
    } else {
        alert(message);
    }
}
