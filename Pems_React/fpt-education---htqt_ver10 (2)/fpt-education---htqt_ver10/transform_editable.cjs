const fs = require('fs');
const file = 'src/pages/dashboard/visit/VisitProcess.tsx';
let content = fs.readFileSync(file, 'utf8');

// Replace readOnly with readOnly={!isInfoEditable} in the whole file
content = content.replace(/readOnly className/g, 'readOnly={!isInfoEditable} className');
content = content.replace(/readOnly value/g, 'readOnly={!isInfoEditable} value');

// Now section 2
const marker = '2. Chuẩn bị chi tiết';
const idx = content.indexOf(marker);

if (idx !== -1) {
    let part1 = content.slice(0, idx);
    let part2 = content.slice(idx);
    
    // First, fix the one input that has disabled={tourGuide !== 'other'}
    part2 = part2.replace(/disabled=\{tourGuide !== 'other'\}/g, "disabled={!isSetupEditable || tourGuide !== 'other'}");
    // Also the button
    // It's fine to combine button as well.
    // wait, I can just do a regex replace for inputs that DO NOT have disabled already.
    part2 = part2.replace(/(<(input|select|textarea)(?![^>]*\bdisabled=)[^>]*?)(?=\s\/?>|>)/g, '$1 disabled={!isSetupEditable}');

    // But regex for replacing without disabled might be slow/wrong.
    // Let's do it carefully.
    
    fs.writeFileSync(file, part1 + part2);
}
