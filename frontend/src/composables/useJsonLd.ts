import { onBeforeUnmount, watchEffect, type Ref } from 'vue'

let counter = 0

/**
 * Keeps a JSON-LD block in `<head>` in sync with a reactive payload.
 *
 * Injected into the head rather than rendered in the template: a
 * `<script type="application/ld+json">` inside a Vue template is not markup Vue
 * will emit, and the workarounds for it are worse than owning the element.
 *
 * Serialised with `JSON.stringify` and assigned to `textContent`, so a product
 * name containing `</script>` or a quote cannot break out of the block.
 */
export function useJsonLd(payload: Ref<unknown>) {
  if (typeof document === 'undefined') return

  const id = `ld-json-${++counter}`

  const element = document.createElement('script')
  element.type = 'application/ld+json'
  element.id = id

  document.head.appendChild(element)

  watchEffect(() => {
    element.textContent = JSON.stringify(payload.value)
  })

  onBeforeUnmount(() => {
    element.remove()
  })
}
