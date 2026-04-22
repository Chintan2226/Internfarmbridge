$(document).ready(function () {
    
    // Changed from kendoGrid to kendoListView for Catalog-style layout
    $("#cropsGrid").kendoListView({
        dataSource: {
            transport: {
                read: {
                    // url: window.API_BASE + "/crops",
                    // dataType: "json"
                }
            },
            data: [
                { Id: 1, Crop: "Premium Wheat", Variety: "Sharbati", Qty: 500, Unit: "Quintals", Price: 2850, HarvestDate: "2024-05-10", Status: "Active", ImageUrl: "https://images.unsplash.com/photo-1542834759-4a9463510522?auto=format&fit=crop&q=80&w=400" },
                { Id: 2, Crop: "Organic Cotton", Variety: "Bt-Hybrid", Qty: 200, Unit: "Quintals", Price: 6500, HarvestDate: "2024-06-15", Status: "Active", ImageUrl: "https://images.unsplash.com/photo-1596700868102-12fbd6dc2d8d?auto=format&fit=crop&q=80&w=400" },
                { Id: 3, Crop: "Soyabean", Variety: "JS-9560", Qty: 150, Unit: "Quintals", Price: 4200, HarvestDate: "2024-05-20", Status: "QC Requested", ImageUrl: "https://images.unsplash.com/photo-1599863261271-eef1b8cae085?auto=format&fit=crop&q=80&w=400" }
            ], // Injecting proxy data to visualize catalog UI immediately
            pageSize: 12
        },
        template: kendo.template($("#crop-card-template").html()),
        autoBind: false
    });
    
    // Force bind initial mock layout
    $("#cropsGrid").data("kendoListView").dataSource.read();

    // Init form widgets (Ready for backend binding)
    $("#cropType").kendoDropDownList({
        dataSource: [], // To be loaded from admin catalog API
        optionLabel: "Select Crop Type..."
    });
    
    $("#cropUnit").kendoDropDownList({
        dataSource: [] // To be loaded from config API
    });
    
    $("#cropQty").kendoNumericTextBox({ min: 1, format: "n0" });
    $("#cropPrice").kendoNumericTextBox({ min: 1, format: "c0" });
    $("#harvestDate").kendoDatePicker({ format: "yyyy-MM-dd", min: new Date() });
    
    $("#cropWindow").kendoWindow({
        width: "600px",
        title: "List New Crop",
        visible: false,
        modal: true,
        actions: ["Close"]
    });
});

function openCropWindow() {
    $("#cropForm")[0].reset();
    $("#cropWindow").data("kendoWindow").center().open();
}

function closeCropWindow() {
    $("#cropWindow").data("kendoWindow").close();
}

function saveCrop(status) {
    // Implementation for calling backend API
    kendo.alert(`Saving listing to backend with status: ${status}...`);
    closeCropWindow();
}

function editCrop(id) {
    openCropWindow();
    // Fetch crop details from backend and populate form
}

function deleteCrop(id) {
    kendo.confirm("Are you sure you want to delete this listing?").then(function () {
        // Implementation for deleting via backend API
        kendo.alert("Listing deleted request sent.");
    });
}

function viewHistory(id) {
    // Fetch status history from backend
    kendo.alert("Loading history...");
}
