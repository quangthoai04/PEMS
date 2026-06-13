const fs = require('fs');
const file = 'src/pages/dashboard/visit/VisitProcess.tsx';
let content = fs.readFileSync(file, 'utf8');

// The marker for section 2 is: "2. Chuẩn bị chi tiết"
const marker = '2. Chuẩn bị chi tiết';
const idx = content.indexOf(marker);

if (idx !== -1) {
    let part1 = content.slice(0, idx);
    let part2 = content.slice(idx);
    
    // Now replace inside part2: update inputs, selects, textareas to disabled={!isSetupEditable}
    // We shouldn't match inputs that already have `readOnly` or `disabled` if we want to be safe,
    // but right now are there any disabled in part2?
    // Let's replace `<input ` with `<input disabled={!isSetupEditable} `
    // Replace `<select ` with `<select disabled={!isSetupEditable} `
    // Replace `<textarea ` with `<textarea disabled={!isSetupEditable} `
    // But there might be `<button disabled={...}>` so be careful.
    
    // We already have some disabled on input?
    part2 = part2.replace(/(<(input|select|textarea)(?!\s(?:type="radio"|type="checkbox")))\s/g, '$1 disabled={!isSetupEditable} ');
    part2 = part2.replace(/<input(?=\s(?:type="radio"|type="checkbox"))\s/g, '<input disabled={!isSetupEditable} ');
    
    // We also need to fix inputs that already had disabled={...} ? Wait, there is one: 
    // <button disabled={tourGuide !== 'other'} -> This is button, we left it alone.
    // What if there is an input with disabled? 
    // Let's write the file.
    
    fs.writeFileSync(file, part1 + part2);
} else {
    console.error("Marker not found");
}
