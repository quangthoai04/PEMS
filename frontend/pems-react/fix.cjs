const fs = require('fs');
const path = require('path');

function walk(dir) {
  let results = [];
  const list = fs.readdirSync(dir);
  list.forEach(file => {
    file = path.join(dir, file);
    const stat = fs.statSync(file);
    if (stat && stat.isDirectory()) {
      results = results.concat(walk(file));
    } else {
      results.push(file);
    }
  });
  return results;
}

const apiFiles = walk('src/features').filter(f => f.endsWith('Api.ts'));
apiFiles.forEach(f => {
  let content = fs.readFileSync(f, 'utf8');
  if(content.includes("import httpClient from '../../shared/api/httpClient'")) {
    content = content.replace(/import httpClient from '\.\.\/\.\.\/shared\/api\/httpClient';/g, "import httpClient from '../../../shared/api/httpClient';");
    fs.writeFileSync(f, content);
    console.log('Fixed API import:', f);
  }
});

const adapterFiles = walk('src/features').filter(f => f.endsWith('Adapter.ts'));
adapterFiles.forEach(f => {
  let content = fs.readFileSync(f, 'utf8');
  if(content.includes("import { AccountManagement }")) {
    content = content.replace(/import \{ AccountManagement \} from '\.\.\/types\/accountManagement\.types';/g, "");
    fs.writeFileSync(f, content);
    console.log('Fixed adapter:', f);
  }
  if(content.includes("import { Authentication }")) {
    content = content.replace(/import \{ Authentication \} from '\.\.\/types\/authentication\.types';/g, "");
    fs.writeFileSync(f, content);
    console.log('Fixed adapter:', f);
  }
});
