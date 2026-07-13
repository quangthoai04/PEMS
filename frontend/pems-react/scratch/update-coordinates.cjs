const https = require('https');
const fs = require('fs');
const path = require('path');

const url = 'https://raw.githubusercontent.com/mledoze/countries/master/countries.json';

https.get(url, (res) => {
    let data = '';
    res.on('data', chunk => data += chunk);
    res.on('end', () => {
        const countries = JSON.parse(data);
        const coords = {};
        
        for (const country of countries) {
            const alpha2 = country.cca2;
            const [lat, lng] = country.latlng;
            if (alpha2 && lat !== undefined && lng !== undefined) {
                coords[alpha2] = { lat, lng };
            }
        }
        
        // Generate the typescript code
        let tsCode = `/**
 * Tọa độ tham chiếu (lat/lng) theo mã quốc gia (ISO 3166-1 alpha-2) 
 * Được lấy tự động để hỗ trợ đầy đủ tất cả quốc gia trên quả cầu.
 */

export interface CountryCoordinate {
  lat: number;
  lng: number;
}

export const ALPHA2_COORDINATES: Record<string, CountryCoordinate> = {
`;
        
        for (const [code, c] of Object.entries(coords)) {
            tsCode += `  '${code}': { lat: ${c.lat}, lng: ${c.lng} },\n`;
        }
        
        tsCode += `};
`;

        const outPath = path.join(__dirname, '../src/shared/constants/countryCoordinatesFull.ts');
        fs.writeFileSync(outPath, tsCode);
        console.log('Successfully wrote', outPath);
    });
}).on('error', (e) => {
    console.error(e);
});
