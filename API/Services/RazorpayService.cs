using Razorpay.Api;
using Microsoft.Extensions.Configuration;

namespace API.Services
{
    public class RazorpayService
    {
        private readonly string _keyId;
        private readonly string _keySecret;

        public RazorpayService(IConfiguration configuration)
        {
            _keyId = configuration["Razorpay:KeyId"] ?? "";
            _keySecret = configuration["Razorpay:KeySecret"] ?? "";
            
            // Add debug log
            Console.WriteLine($"Razorpay initialized with KeyId: {(_keyId?.Substring(0, 10) ?? "null")}...");
        }

        private RazorpayClient GetClient()
        {
            return new RazorpayClient(_keyId, _keySecret);
        }

        public async Task<dynamic> CreateOrderAsync(decimal amount, string receipt)
        {
            try
            {
                int amountInPaise = (int)(amount * 100);
                
                var client = GetClient();
                
                Dictionary<string, object> options = new Dictionary<string, object>
                {
                    { "amount", amountInPaise },
                    { "currency", "INR" },
                    { "receipt", receipt },
                    { "payment_capture", 1 }
                };
                
                Console.WriteLine($"Creating Razorpay order: Amount={amountInPaise} paise, Receipt={receipt}");
                
                dynamic order = await Task.Run(() => client.Order.Create(options));
                
                Console.WriteLine($"Razorpay order created: {order["id"]}");
                return order;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Razorpay order creation failed: {ex.Message}");
                throw new Exception($"Razorpay order creation failed: {ex.Message}");
            }
        }

        public bool VerifyPaymentSignature(string orderId, string paymentId, string signature)
        {
            try
            {
                Console.WriteLine($"Verifying signature: OrderId={orderId}, PaymentId={paymentId}, Signature={signature?.Substring(0, 10)}...");
                
                Dictionary<string, string> attributes = new Dictionary<string, string>
                {
                    { "razorpay_order_id", orderId },
                    { "razorpay_payment_id", paymentId },
                    { "razorpay_signature", signature }
                };
                
                Utils.verifyPaymentSignature(attributes);
                
                Console.WriteLine("Signature verification SUCCESS");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Signature verification FAILED: {ex.Message}");
                return false;
            }
        }
    }
}