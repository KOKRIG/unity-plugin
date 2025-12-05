#!/usr/bin/env node
/**
 * Create Unity Package (.unitypackage) without Unity Editor
 * Unity packages are tar.gz archives with a specific structure.
 */

const fs = require('fs');
const path = require('path');
const zlib = require('zlib');
const { execSync } = require('child_process');

const PACKAGE_NAME = 'Deffatest_v1.0.0.unitypackage';
const ASSETS_FOLDER = 'Assets/Deffatest';

// Extract GUID from meta file
function extractGuid(metaFilePath) {
    try {
        const content = fs.readFileSync(metaFilePath, 'utf8');
        const match = content.match(/guid:\s*([a-f0-9]+)/);
        return match ? match[1] : null;
    } catch (e) {
        console.error(`Error reading ${metaFilePath}: ${e.message}`);
        return null;
    }
}

// Get all assets recursively
function getAllAssets(basePath) {
    const assets = [];
    
    function walkDir(dir) {
        const items = fs.readdirSync(dir);
        
        for (const item of items) {
            const fullPath = path.join(dir, item);
            const stat = fs.statSync(fullPath);
            
            if (stat.isDirectory()) {
                // Check for directory meta file
                const dirMeta = fullPath + '.meta';
                if (fs.existsSync(dirMeta)) {
                    assets.push({
                        path: fullPath.replace(/\\/g, '/'),
                        meta: dirMeta,
                        isFolder: true,
                        asset: null
                    });
                }
                walkDir(fullPath);
            } else if (!item.endsWith('.meta')) {
                // Regular file
                const fileMeta = fullPath + '.meta';
                if (fs.existsSync(fileMeta)) {
                    assets.push({
                        path: fullPath.replace(/\\/g, '/'),
                        meta: fileMeta,
                        isFolder: false,
                        asset: fullPath
                    });
                }
            }
        }
    }
    
    // Add root folder meta
    const rootMeta = basePath + '.meta';
    if (fs.existsSync(rootMeta)) {
        assets.push({
            path: basePath.replace(/\\/g, '/'),
            meta: rootMeta,
            isFolder: true,
            asset: null
        });
    }
    
    walkDir(basePath);
    return assets;
}

// Create tar archive manually (simple implementation)
function createTarEntry(name, content, isDir = false) {
    const header = Buffer.alloc(512, 0);
    
    // File name (100 bytes)
    const nameBytes = Buffer.from(name.substring(0, 99));
    nameBytes.copy(header, 0);
    
    // File mode (8 bytes)
    const mode = isDir ? '0000755' : '0000644';
    Buffer.from(mode + ' ').copy(header, 100);
    
    // UID (8 bytes)
    Buffer.from('0000000 ').copy(header, 108);
    
    // GID (8 bytes)
    Buffer.from('0000000 ').copy(header, 116);
    
    // File size (12 bytes)
    const size = content ? content.length : 0;
    const sizeStr = size.toString(8).padStart(11, '0') + ' ';
    Buffer.from(sizeStr).copy(header, 124);
    
    // Modification time (12 bytes)
    const mtime = Math.floor(Date.now() / 1000).toString(8).padStart(11, '0') + ' ';
    Buffer.from(mtime).copy(header, 136);
    
    // Checksum placeholder (8 bytes)
    Buffer.from('        ').copy(header, 148);
    
    // Type flag (1 byte)
    header[156] = isDir ? 53 : 48; // '5' for dir, '0' for file
    
    // Calculate checksum
    let checksum = 0;
    for (let i = 0; i < 512; i++) {
        checksum += header[i];
    }
    const checksumStr = checksum.toString(8).padStart(6, '0') + '\0 ';
    Buffer.from(checksumStr).copy(header, 148);
    
    // Prepare data blocks
    const blocks = [header];
    
    if (content && content.length > 0) {
        // Add content with padding to 512-byte blocks
        const paddingSize = 512 - (content.length % 512);
        const paddedContent = Buffer.concat([
            Buffer.isBuffer(content) ? content : Buffer.from(content),
            Buffer.alloc(paddingSize === 512 ? 0 : paddingSize, 0)
        ]);
        blocks.push(paddedContent);
    }
    
    return Buffer.concat(blocks);
}

function createUnityPackage(outputPath, assetsFolder) {
    console.log(`Creating Unity Package: ${outputPath}`);
    console.log(`Assets folder: ${assetsFolder}`);
    
    const assets = getAllAssets(assetsFolder);
    console.log(`Found ${assets.length} assets\n`);
    
    const tarParts = [];
    
    for (const asset of assets) {
        const guid = extractGuid(asset.meta);
        if (!guid) {
            console.log(`  Skipping (no GUID): ${asset.path}`);
            continue;
        }
        
        console.log(`  Adding: ${asset.path} (GUID: ${guid.substring(0, 8)}...)`);
        
        // Create directory entry
        tarParts.push(createTarEntry(`${guid}/`, null, true));
        
        // Add pathname file
        tarParts.push(createTarEntry(`${guid}/pathname`, asset.path));
        
        // Add asset.meta
        const metaContent = fs.readFileSync(asset.meta);
        tarParts.push(createTarEntry(`${guid}/asset.meta`, metaContent));
        
        // Add asset file (if not a folder)
        if (!asset.isFolder && asset.asset) {
            const assetContent = fs.readFileSync(asset.asset);
            tarParts.push(createTarEntry(`${guid}/asset`, assetContent));
        }
    }
    
    // Add tar end blocks (two 512-byte zero blocks)
    tarParts.push(Buffer.alloc(1024, 0));
    
    // Combine all parts
    const tarBuffer = Buffer.concat(tarParts);
    
    // Gzip compress
    console.log(`\nCompressing archive...`);
    const gzipped = zlib.gzipSync(tarBuffer);
    
    // Write output
    fs.writeFileSync(outputPath, gzipped);
    
    const sizeKB = (fs.statSync(outputPath).size / 1024).toFixed(1);
    console.log(`\n✅ Package created successfully!`);
    console.log(`   Output: ${path.resolve(outputPath)}`);
    console.log(`   Size: ${sizeKB} KB`);
}

// Run
createUnityPackage(PACKAGE_NAME, ASSETS_FOLDER);
