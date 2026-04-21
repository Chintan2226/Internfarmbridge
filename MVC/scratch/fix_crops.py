
import os

path = r'c:\Casepoint\GROUP_A_Project\FarmBridge\Internfarmbridge\MVC\Views\Farmer\Crops.cshtml'

# Read as bytes to avoid encoding issues
with open(path, 'rb') as f:
    data = f.read()

# Fix 1: The Modal Opening
# Target: <!-- ... Create / Edit Modal ... <!-- Section: Product -->
# We'll be generous with the pattern to catch the corruption
import re

# This pattern looks for the comment that contains 'Create / Edit Modal' 
# and goes all the way to '<!-- Section: Product -->'
# The corruption seems to have merged them.
pattern1 = b'<!-- [^>]* Create / Edit Modal [^>]* <!-- Section: Product -->'
replacement1 = b'<div id="cropWindow" style="display:none;">\r\n    <h3 id="modalTitle" style="display:none;">List New Crop</h3>\r\n    <form id="cropForm">\r\n        <div class="modal-body-content">\r\n            <!-- Section: Product -->'

if re.search(pattern1, data):
    data = re.sub(pattern1, replacement1, data)
    print("Fixed modal opening.")
else:
    # Try a slightly different pattern if the above fails
    pattern1_alt = b'<!-- .*? Create / Edit Modal .*? <!-- Section: Product -->'
    if re.search(pattern1_alt, data, re.DOTALL):
        data = re.sub(pattern1_alt, replacement1, data, flags=re.DOTALL)
        print("Fixed modal opening (alt).")
    else:
        print("Could not find modal opening pattern.")

# Fix 2: Logistics duplication
# The corruption looks like:
# </div>
#             </div>                                  padding:8px 12px; font-size:13px; resize:none;"
#                               placeholder="Village, Taluka, full address..."></textarea>
#                 </div>
#                 <div class="col-md-6">
#                     <label class="form-lbl">Additional Notes <span class="lbl-opt">(Optional)</span></label>
#                     <textarea class="k-textarea" id="cropNotes" rows="1"
#                               style="width:100%; border-radius:10px; border:1px solid #e5e7eb;
#                                      padding:8px 12px; font-size:13px; resize:none;"
#                               placeholder="Any specific details about the crop batch..."></textarea>
#                 </div>
#             </div>
#
#         </div>

# I'll look for that specific mess.
pattern2 = b'</div>\s*</div>\s*</div>\s*padding:8px 12px; font-size:13px; resize:none;"\s*placeholder="Village, Taluka, full address..."></textarea>\s*</div>\s*<div class="col-md-6">\s*<label class="form-lbl">Additional Notes <span class="lbl-opt">\(Optional\)</span></label>\s*<textarea class="k-textarea" id="cropNotes" rows="1"\s*style="width:100%; border-radius:10px; border:1px solid #e5e7eb;\s*padding:8px 12px; font-size:13px; resize:none;"\s*placeholder="Any specific details about the crop batch..."></textarea>\s*</div>\s*</div>\s*</div>'

# Wait, that's too specific and might fail if spaces differ.
# Let's look for the start of the mess: '</div>\s*</div>\s*</div>\s*padding:8px 12px' 
# and the end: '</div>\s*</div>\s*</div>\s*<!-- Footer Buttons -->'

pattern2_optimized = b'</div>\s*</div>\s*</div>\s*padding:8px 12px; font-size:13px; resize:none;".*?</div>\s*</div>\s*</div>\s*(?=\s*<!-- Footer Buttons -->)'
replacement2 = b'</div>\r\n                </div>\r\n            </div>\r\n        </div>\r\n'

if re.search(pattern2_optimized, data, re.DOTALL):
    data = re.sub(pattern2_optimized, replacement2, data, flags=re.DOTALL)
    print("Fixed logistics duplication.")
else:
    print("Could not find logistics pattern.")

with open(path, 'wb') as f:
    f.write(data)
