/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** WhatsApp number used for the order handoff, digits only. */
  readonly VITE_WHATSAPP_NUMBER?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
