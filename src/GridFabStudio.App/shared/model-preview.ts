import * as THREE from "three";
import { OrbitControls } from "three/addons/controls/OrbitControls.js";

export function createPreview(host: HTMLDivElement, model: THREE.Object3D, size: THREE.Vector3, disposeModel: () => void, assembly = false) {
    const renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
    const scene = new THREE.Scene();
    scene.background = new THREE.Color('#1b2029');
    scene.add(model);
    scene.add(new THREE.HemisphereLight(0xffffff, 0x577074, 2));
    const key = new THREE.DirectionalLight(0xffffff, 2.5);
    key.position.set(2, 4, 3);
    scene.add(key);
    const scale = size.length();
    const camera = new THREE.PerspectiveCamera(38, 1, 0.1, scale * 30);
    const controls = new OrbitControls(camera, renderer.domElement);
    const render = () => renderer.render(scene, camera);
    const resize = () => {
      camera.aspect = Math.max(1, host.clientWidth) / Math.max(1, host.clientHeight);
      camera.updateProjectionMatrix();
      renderer.setSize(host.clientWidth, host.clientHeight);
      render();
    };
    camera.position.set(1, 0.8, 1.2).normalize().multiplyScalar(scale * (assembly ? 1.75 : 2));
    controls.update();
    controls.addEventListener('change', render);
    const observer = new ResizeObserver(resize);
    observer.observe(host);
    host.appendChild(renderer.domElement);
    const dispose = () => {
      observer.disconnect();
      controls.dispose();
      disposeModel();
      renderer.dispose();
      renderer.domElement.remove();
    };
    resize();
    return { render, dispose };
  }
