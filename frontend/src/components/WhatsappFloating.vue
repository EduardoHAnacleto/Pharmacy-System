<template>
  <a
    v-if="href"
    :href="href"
    target="_blank"
    rel="noopener"
    class="whatsapp-float"
    :aria-label="t('contact.whatsapp')"
  >
    <i class="bi bi-whatsapp" aria-hidden="true"></i>
  </a>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useSettingsStore } from '@/stores/settings'

const { t } = useI18n()
const settings = useSettingsStore()

/**
 * Nothing is rendered when the shop has no WhatsApp number.
 *
 * The number used to be hardcoded here — and to a different number than the one
 * the checkout used, so the two contradicted each other. A button that opens an
 * empty chat is worse than no button.
 */
const href = computed(() => {
  const number = settings.whatsAppNumber
  if (!number) return null

  return `https://wa.me/${number}?text=${encodeURIComponent(t('contact.whatsappGreeting'))}`
})
</script>

<style scoped>
.whatsapp-float {
  position: fixed;
  width: 56px;
  height: 56px;
  bottom: 20px;
  right: 20px;
  background-color: #25d366;
  color: #fff;
  border-radius: 50%;
  text-align: center;
  font-size: 32px;
  line-height: 56px;
  z-index: 1000;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.3);
}

.whatsapp-float:hover {
  color: #fff;
  background-color: #1ebe5d;
}
</style>
