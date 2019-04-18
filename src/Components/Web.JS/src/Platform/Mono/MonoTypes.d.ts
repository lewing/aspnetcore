declare namespace Module {
  function UTF8ToString(utf8: Mono.Utf8Ptr): string;
  var preloadPlugins: any[];

  function stackSave(): Mono.StackSaveHandle;
  function stackAlloc(length: number): number;
  function stackRestore(handle: Mono.StackSaveHandle): void;

  // These should probably be in @types/emscripten
  function FS_createPath(parent, path, canRead, canWrite);
  function FS_createDataFile(parent, name, data, canRead, canWrite, canOwn);
  function onRuntimeInitialized(cb: () => any): void;
}

// Emscripten declares these globals
declare const addRunDependency: any;
declare const removeRunDependency: any;

declare namespace Mono {
  interface Utf8Ptr { Utf8Ptr__DO_NOT_IMPLEMENT: any }
  interface StackSaveHandle { StackSaveHandle__DO_NOT_IMPLEMENT: any }
}

// Mono uses this global to hang various debugging-related items on
declare namespace MONO {
  var loaded_files: string[];
  var mono_wasm_runtime_is_ready: boolean;
  function mono_wasm_setenv(name: string, value: string): void;
  function mono_load_runtime_and_bcl(vfs_prefix: string, deploy_prefix: string, enable_debugging: boolean, file_list: string[], loaded_cb: any, fetch_file_cb: any): void;
  function mono_wasm_set_runtime_options(options: string[]): void;
}
