const fs = require('fs');

let content = fs.readFileSync('src/pages/dashboard/visit/VisitProcess.tsx', 'utf8');

// 1. Fix tour guide display to match CreateVisitRequest
const oldTourGuideDisplay = `{addedTourGuides.length > 0 && tourGuide === 'other' && (
                            <div className="mt-4 flex flex-wrap gap-2">
                              {addedTourGuides.map((guide) => (
                                <div key={guide} className="flex items-center gap-2 bg-white px-3 py-1.5 rounded-full border border-gray-200 shadow-sm text-sm">
                                  <div className="w-6 h-6 rounded-full bg-[#004c91] text-white flex items-center justify-center font-bold text-xs">
                                    {guide.charAt(0)}
                                  </div>
                                  <span className="font-medium text-gray-700">{guide}</span>
                                  {isSetupEditable && (
                                    <button
                                      type="button"
                                      onClick={() => setAddedTourGuides(addedTourGuides.filter((g) => g !== guide))}
                                      className="w-5 h-5 rounded-full hover:bg-red-50 text-gray-400 hover:text-red-500 flex items-center justify-center transition-colors outline-none ml-1"
                                    >
                                      <X className="w-3 h-3" />
                                    </button>
                                  )}
                                </div>
                              ))}
                            </div>
                          )}`;

const newTourGuideDisplay = `{addedTourGuides.length > 0 && tourGuide === 'other' && (
                            <div className="mt-3 space-y-2">
                              {addedTourGuides.map((guide) => (
                                <div key={guide} className="flex items-center justify-between p-3 bg-gray-50 rounded-lg border border-gray-100">
                                  <div className="flex items-center gap-3">
                                    <div className="w-8 h-8 rounded-full bg-[#004c91] text-white flex items-center justify-center font-bold text-xs">
                                      {guide.charAt(0)}
                                    </div>
                                    <div>
                                      <div className="text-sm font-bold text-gray-900">{guide}</div>
                                      <div className="text-xs text-[#004c91] font-medium flex items-center gap-1">
                                        Đã thêm
                                      </div>
                                    </div>
                                  </div>
                                  {isSetupEditable && (
                                    <button
                                      type="button"
                                      onClick={() => setAddedTourGuides(addedTourGuides.filter((g) => g !== guide))}
                                      className="p-1.5 text-gray-400 hover:text-red-500 rounded-lg hover:bg-red-50 transition-colors"
                                    >
                                      <X className="w-4 h-4" />
                                    </button>
                                  )}
                                </div>
                              ))}
                            </div>
                          )}`;

content = content.replace(oldTourGuideDisplay, newTourGuideDisplay);

// Replace flex container for input groups to break into column on small screens
// I need to be careful with string replacements to not break other things

// Look at typical Xe dien layouts:
content = content.replace(/className="flex items-center gap-2 mt-2"/g, 'className="flex flex-col sm:flex-row sm:items-center gap-2 mt-2"');
// Look at other flex items
content = content.replace(/<div className="flex items-center gap-2 w-full max-w-lg">/g, '<div className="flex flex-col sm:flex-row sm:items-center gap-2 w-full max-w-lg">');

// For "Lượt đi" inputs
content = content.replace(/className="flex items-center gap-2"/g, 'className="flex flex-col sm:flex-row sm:items-center gap-2"');

fs.writeFileSync('src/pages/dashboard/visit/VisitProcess.tsx', content);
