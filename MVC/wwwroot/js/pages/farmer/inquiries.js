$(document).ready(function () {
    
    $("#messageBody").kendoTextArea({
        placeholder: "Please describe your issue or question in detail. Always explicitly mention Order IDs or Crop Listings if relevant.",
        rows: 5
    });
});

function submitInquiry() {
    var type = $("#inquiryType").val();
    var priority = $("#inquiryPriority").val();
    var subject = $("#subject").val();
    var message = $("#messageBody").val();

    if (!type || !priority || !subject || !message) {
        kendo.alert("Please fill out all required fields (Department, Priority, Subject, and Details) before submitting the ticket.");
        return;
    }

    kendo.confirm("Send this support ticket to the helpdesk?").then(function () {
        kendo.alert("Your ticket has been officially logged in our system. The relevant department will respond within your selected SLA window.");
        $("#subject").val("");
        $("#messageBody").data("kendoTextArea").value("");
        $("#inquiryType").val("");
        $("#inquiryPriority").val("");
    });
}
