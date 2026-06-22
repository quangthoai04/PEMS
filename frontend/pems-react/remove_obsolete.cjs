const fs = require('fs');
const filePath = 'd:/ki9/PEMS/frontend/pems-react/src/pages/dashboard/home/SharedDashboardView.tsx';
let content = fs.readFileSync(filePath, 'utf8');

const blockStartAnchor = '<div id="staff-leader-popover-regular-details"';
const blockStartTarget = "{activePopoverEvent.category === 'Lời mời tham gia' && (";
const blockStartIndex = content.lastIndexOf(blockStartTarget, content.indexOf(blockStartAnchor));

if (blockStartIndex !== -1) {
    const endAnchor = '            {/* Footer controls inside modal */}';
    const endIndex = content.indexOf(endAnchor, blockStartIndex);
    if (endIndex !== -1) {
        // Find the </div> just before the footer
        const closeDivIndex = content.lastIndexOf('</div>', endIndex);
        if (closeDivIndex !== -1 && closeDivIndex > blockStartIndex) {
            content = content.substring(0, blockStartIndex) + content.substring(closeDivIndex + 6);
            fs.writeFileSync(filePath, content, 'utf8');
            console.log('Removed obsolete bottom block!');
        } else {
             content = content.substring(0, blockStartIndex) + content.substring(endIndex);
             fs.writeFileSync(filePath, content, 'utf8');
             console.log('Removed obsolete bottom block directly to footer!');
        }
    } else {
        console.log('Could not find endAnchor');
    }
} else {
    console.log('Could not find blockStartIndex');
}
