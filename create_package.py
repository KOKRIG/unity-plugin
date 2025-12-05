#!/usr/bin/env python3
"""
Create Unity Package (.unitypackage) without Unity Editor
Unity packages are tar.gz archives with a specific structure.
"""

import os
import tarfile
import tempfile
import shutil
import re

# Configuration
PACKAGE_NAME = "Deffatest_v1.0.0.unitypackage"
ASSETS_FOLDER = "Assets/Deffatest"

def extract_guid(meta_file_path):
    """Extract GUID from a .meta file"""
    try:
        with open(meta_file_path, 'r', encoding='utf-8') as f:
            content = f.read()
            match = re.search(r'guid:\s*([a-f0-9]+)', content)
            if match:
                return match.group(1)
    except Exception as e:
        print(f"Error reading {meta_file_path}: {e}")
    return None

def get_all_assets(base_path):
    """Get all assets and their meta files"""
    assets = []
    
    for root, dirs, files in os.walk(base_path):
        # Add directory meta files
        dir_meta = root + ".meta"
        if os.path.exists(dir_meta):
            rel_path = root.replace("\\", "/")
            assets.append({
                'path': rel_path,
                'meta': dir_meta,
                'is_folder': True,
                'asset': None
            })
        
        # Add file assets
        for file in files:
            if file.endswith('.meta'):
                continue
            
            file_path = os.path.join(root, file)
            meta_path = file_path + ".meta"
            
            if os.path.exists(meta_path):
                rel_path = file_path.replace("\\", "/")
                assets.append({
                    'path': rel_path,
                    'meta': meta_path,
                    'is_folder': False,
                    'asset': file_path
                })
    
    # Also add the root folder meta
    root_meta = base_path + ".meta"
    if os.path.exists(root_meta):
        assets.insert(0, {
            'path': base_path.replace("\\", "/"),
            'meta': root_meta,
            'is_folder': True,
            'asset': None
        })
    
    return assets

def create_unitypackage(output_path, assets_folder):
    """Create a .unitypackage file"""
    
    print(f"Creating Unity Package: {output_path}")
    print(f"Assets folder: {assets_folder}")
    
    # Get all assets
    assets = get_all_assets(assets_folder)
    print(f"Found {len(assets)} assets")
    
    # Create temp directory for package structure
    temp_dir = tempfile.mkdtemp()
    
    try:
        for asset in assets:
            guid = extract_guid(asset['meta'])
            if not guid:
                print(f"  Skipping (no GUID): {asset['path']}")
                continue
            
            print(f"  Adding: {asset['path']} (GUID: {guid[:8]}...)")
            
            # Create GUID folder
            guid_folder = os.path.join(temp_dir, guid)
            os.makedirs(guid_folder, exist_ok=True)
            
            # Write pathname file
            pathname_file = os.path.join(guid_folder, "pathname")
            with open(pathname_file, 'w', encoding='utf-8') as f:
                f.write(asset['path'])
            
            # Copy meta file as asset.meta
            meta_dest = os.path.join(guid_folder, "asset.meta")
            shutil.copy2(asset['meta'], meta_dest)
            
            # Copy actual asset file (if not a folder)
            if not asset['is_folder'] and asset['asset']:
                asset_dest = os.path.join(guid_folder, "asset")
                shutil.copy2(asset['asset'], asset_dest)
        
        # Create tar.gz archive
        print(f"\nCreating archive...")
        with tarfile.open(output_path, "w:gz") as tar:
            for item in os.listdir(temp_dir):
                item_path = os.path.join(temp_dir, item)
                tar.add(item_path, arcname=item)
        
        print(f"\n✅ Package created successfully!")
        print(f"   Output: {os.path.abspath(output_path)}")
        print(f"   Size: {os.path.getsize(output_path) / 1024:.1f} KB")
        
    finally:
        # Cleanup temp directory
        shutil.rmtree(temp_dir)

if __name__ == "__main__":
    # Change to script directory
    script_dir = os.path.dirname(os.path.abspath(__file__))
    os.chdir(script_dir)
    
    # Create package
    create_unitypackage(PACKAGE_NAME, ASSETS_FOLDER)
