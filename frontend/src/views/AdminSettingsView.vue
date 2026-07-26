<template>
  <section class="py-4">
    <div class="container">
      <!-- HEADER -->
      <div class="d-flex justify-content-between align-items-center mb-3">
        <h3 class="mb-0">{{ t('admin.settingsTitle') }}</h3>
        <RouterLink class="btn btn-outline-secondary btn-sm" to="/admin">
          {{ t('common.back') }}
        </RouterLink>
      </div>

      <p class="text-muted">{{ t('admin.settingsIntro') }}</p>

      <div v-if="error" class="alert alert-danger">{{ error }}</div>
      <div v-if="saved" class="alert alert-success">{{ t('admin.saved') }}</div>

      <form @submit.prevent="save">
        <!-- IDENTITY -->
        <div class="card mb-3">
          <div class="card-body row g-3">
            <h5 class="mb-0">{{ t('admin.identity') }}</h5>

            <div class="col-md-6">
              <label class="form-label" for="store-name">{{ t('admin.storeName') }}</label>
              <input
                id="store-name"
                v-model="form.storeName"
                class="form-control"
                maxlength="100"
                required
              />
            </div>

            <div class="col-md-6">
              <label class="form-label" for="tagline">{{ t('admin.tagline') }}</label>
              <input id="tagline" v-model="taglineInput" class="form-control" maxlength="200" />
            </div>

            <div class="col-md-6">
              <label class="form-label" for="logo-url">{{ t('admin.logoUrl') }}</label>
              <input id="logo-url" v-model="logoInput" class="form-control" maxlength="255" />
            </div>

            <div class="col-md-3">
              <label class="form-label" for="primary-color">{{ t('admin.primaryColor') }}</label>
              <input
                id="primary-color"
                v-model="form.primaryColor"
                type="color"
                class="form-control form-control-color"
              />
            </div>

            <div class="col-md-3">
              <label class="form-label" for="secondary-color">{{
                t('admin.secondaryColor')
              }}</label>
              <input
                id="secondary-color"
                v-model="form.secondaryColor"
                type="color"
                class="form-control form-control-color"
              />
            </div>
          </div>
        </div>

        <!-- LOCATION -->
        <div class="card mb-3">
          <div class="card-body row g-3">
            <h5 class="mb-0">{{ t('admin.location') }}</h5>

            <div class="col-md-6">
              <label class="form-label" for="address">{{ t('admin.address') }}</label>
              <input id="address" v-model="addressInput" class="form-control" maxlength="200" />
            </div>

            <div class="col-md-3">
              <label class="form-label" for="settings-city">{{ t('admin.city') }}</label>
              <input id="settings-city" v-model="cityInput" class="form-control" maxlength="100" />
            </div>

            <div class="col-md-3">
              <label class="form-label" for="country">{{ t('admin.countryCode') }}</label>
              <input
                id="country"
                v-model="form.countryCode"
                class="form-control text-uppercase"
                maxlength="2"
                required
              />
            </div>

            <div class="col-md-6">
              <label class="form-label" for="time-zone">{{ t('admin.timeZone') }}</label>
              <input id="time-zone" v-model="form.timeZone" class="form-control" required />
            </div>

            <div class="col-md-6">
              <label class="form-label" for="map-query">{{ t('admin.mapQuery') }}</label>
              <input id="map-query" v-model="mapInput" class="form-control" maxlength="200" />
            </div>
          </div>
        </div>

        <!-- CONTACT -->
        <div class="card mb-3">
          <div class="card-body row g-3">
            <h5 class="mb-0">{{ t('admin.contact') }}</h5>

            <div class="col-md-4">
              <label class="form-label" for="settings-phone">{{ t('admin.phone') }}</label>
              <input id="settings-phone" v-model="phoneInput" class="form-control" maxlength="30" />
            </div>

            <div class="col-md-4">
              <label class="form-label" for="whatsapp">{{ t('admin.whatsAppNumber') }}</label>
              <input
                id="whatsapp"
                v-model="whatsAppInput"
                class="form-control"
                inputmode="numeric"
                maxlength="20"
                :readonly="whatsAppManaged"
              />
              <!-- Said plainly rather than left to be discovered: editing a field
                   that has no effect reads as a broken save. -->
              <small v-if="whatsAppManaged" class="form-text text-warning">
                {{ t('admin.whatsAppManaged') }}
              </small>
            </div>

            <div class="col-md-4">
              <label class="form-label" for="settings-email">{{ t('admin.email') }}</label>
              <input
                id="settings-email"
                v-model="emailInput"
                type="email"
                class="form-control"
                maxlength="150"
              />
            </div>

            <div class="col-md-6">
              <label class="form-label" for="instagram">{{ t('admin.instagramUrl') }}</label>
              <input id="instagram" v-model="instagramInput" class="form-control" maxlength="255" />
            </div>

            <div class="col-md-6">
              <label class="form-label" for="facebook">{{ t('admin.facebookUrl') }}</label>
              <input id="facebook" v-model="facebookInput" class="form-control" maxlength="255" />
            </div>
          </div>
        </div>

        <!-- COMMERCE -->
        <div class="card mb-3">
          <div class="card-body row g-3">
            <h5 class="mb-0">{{ t('admin.commerce') }}</h5>

            <div class="col-md-3">
              <label class="form-label" for="currency">{{ t('admin.currency') }}</label>
              <input
                id="currency"
                v-model="form.currency"
                class="form-control text-uppercase"
                maxlength="3"
                required
              />
            </div>

            <div class="col-md-3">
              <label class="form-label" for="locale">{{ t('admin.locale') }}</label>
              <select id="locale" v-model="form.locale" class="form-select">
                <option v-for="option in SUPPORTED_LOCALES" :key="option" :value="option">
                  {{ option }}
                </option>
              </select>
            </div>

            <div class="col-md-3">
              <label class="form-label" for="delivery-fee">{{ t('admin.deliveryFee') }}</label>
              <input
                id="delivery-fee"
                v-model.number="form.deliveryFee"
                type="number"
                step="0.01"
                min="0"
                class="form-control"
              />
            </div>

            <div class="col-md-3">
              <label class="form-label" for="min-delivery">{{ t('admin.minDeliveryTotal') }}</label>
              <input
                id="min-delivery"
                v-model.number="form.minDeliveryTotal"
                type="number"
                step="0.01"
                min="0"
                class="form-control"
              />
            </div>

            <div class="col-md-6">
              <div class="form-check">
                <input
                  id="delivery-enabled"
                  v-model="form.deliveryEnabled"
                  class="form-check-input"
                  type="checkbox"
                />
                <label class="form-check-label" for="delivery-enabled">
                  {{ t('admin.deliveryEnabled') }}
                </label>
              </div>

              <div class="form-check">
                <input
                  id="pickup-enabled"
                  v-model="form.pickupEnabled"
                  class="form-check-input"
                  type="checkbox"
                />
                <label class="form-check-label" for="pickup-enabled">
                  {{ t('admin.pickupEnabled') }}
                </label>
              </div>
            </div>

            <div class="col-md-6">
              <label class="form-label" for="delivery-cities">
                {{ t('admin.deliveryCities') }}
              </label>
              <textarea
                id="delivery-cities"
                v-model="citiesText"
                class="form-control"
                rows="3"
              ></textarea>
            </div>
          </div>
        </div>

        <!-- CHECKOUT -->
        <div class="card mb-3">
          <div class="card-body">
            <h5 class="mb-3">{{ t('admin.checkoutFields') }}</h5>

            <div class="form-check">
              <input
                id="collect-tax-id"
                v-model="form.collectTaxId"
                class="form-check-input"
                type="checkbox"
              />
              <label class="form-check-label" for="collect-tax-id">
                {{ t('admin.collectTaxId') }}
              </label>
            </div>

            <div class="form-check">
              <input
                id="collect-postal"
                v-model="form.collectPostalCode"
                class="form-check-input"
                type="checkbox"
              />
              <label class="form-check-label" for="collect-postal">
                {{ t('admin.collectPostalCode') }}
              </label>
            </div>
          </div>
        </div>

        <!-- OPERATION -->
        <div class="card mb-3">
          <div class="card-body">
            <h5 class="mb-3">{{ t('admin.openingHours') }}</h5>

            <div v-for="weekday in weekdays" :key="weekday" class="row g-2 align-items-center mb-2">
              <div class="col-md-2">
                <strong class="small">{{ t(`weekday.${weekday}`) }}</strong>
              </div>

              <div class="col-md-10">
                <div
                  v-for="(range, index) in rangesFor(weekday)"
                  :key="index"
                  class="d-flex align-items-center gap-2 mb-1"
                >
                  <input
                    v-model="range.open"
                    type="time"
                    class="form-control form-control-sm"
                    style="max-width: 8rem"
                    :aria-label="`${t('weekday.' + weekday)} ${t('contact.open')}`"
                  />
                  <span>–</span>
                  <input
                    v-model="range.close"
                    type="time"
                    class="form-control form-control-sm"
                    style="max-width: 8rem"
                    :aria-label="`${t('weekday.' + weekday)} ${t('contact.closed')}`"
                  />
                  <button
                    type="button"
                    class="btn btn-sm btn-outline-danger"
                    @click="removeRange(weekday, index)"
                  >
                    {{ t('common.remove') }}
                  </button>
                </div>

                <button
                  type="button"
                  class="btn btn-sm btn-outline-secondary"
                  @click="addRange(weekday)"
                >
                  {{ t('admin.addRange') }}
                </button>
              </div>
            </div>

            <div class="mt-3">
              <label class="form-label" for="footer-text">{{ t('admin.footerText') }}</label>
              <input id="footer-text" v-model="footerInput" class="form-control" maxlength="300" />
            </div>
          </div>
        </div>

        <button class="btn btn-primary" type="submit" :disabled="saving">
          {{ saving ? t('common.saving') : t('common.save') }}
        </button>
      </form>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { RouterLink } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useSettingsStore } from '@/stores/settings'
import { SUPPORTED_LOCALES } from '@/i18n'
import type { OpeningRange, StoreSettings } from '@/types/storeSettings'

const { t } = useI18n()
const settings = useSettingsStore()

const weekdays = [1, 2, 3, 4, 5, 6, 7]

const form = reactive<StoreSettings>(clone(settings.settings))

/** Whether Store:WhatsAppNumber in the environment is overriding the stored row. */
const whatsAppManaged = computed(() => settings.settings.whatsAppNumberIsManagedByEnvironment)

const saving = ref(false)
const saved = ref(false)
const error = ref<string | null>(null)

/**
 * Text inputs bind through these rather than to the nullable fields directly.
 *
 * An empty text input yields `''`, and sending `''` where the API expects null
 * would store empty strings that then render as blank lines on the storefront.
 */
const taglineInput = nullable('tagline')
const logoInput = nullable('logoUrl')
const addressInput = nullable('address')
const cityInput = nullable('city')
const mapInput = nullable('mapQuery')
const phoneInput = nullable('phone')
const whatsAppInput = nullable('whatsAppNumber')
const emailInput = nullable('email')
const instagramInput = nullable('instagramUrl')
const facebookInput = nullable('facebookUrl')
const footerInput = nullable('footerText')

function nullable(key: keyof StoreSettings) {
  return computed({
    get: () => (form[key] as string | null) ?? '',
    set: (value: string) => {
      // @ts-expect-error — every key passed here is a `string | null` field.
      form[key] = value.trim() === '' ? null : value
    },
  })
}

/** One city per line reads better in a textarea than a comma-separated string. */
const citiesText = computed({
  get: () => form.deliveryCities.join('\n'),
  set: (value: string) => {
    form.deliveryCities = value
      .split('\n')
      .map((c) => c.trim())
      .filter((c) => c.length > 0)
  },
})

onMounted(async () => {
  if (!settings.loaded) await settings.load()
  Object.assign(form, clone(settings.settings))
})

// The store is also what the rest of the app renders from, so a reload elsewhere
// should refresh this form as long as it has not been edited yet.
watch(
  () => settings.settings,
  (value) => {
    if (!saving.value && !saved.value) Object.assign(form, clone(value))
  },
)

function rangesFor(weekday: number): OpeningRange[] {
  form.openingHours[String(weekday)] ??= []
  return form.openingHours[String(weekday)]!
}

function addRange(weekday: number) {
  rangesFor(weekday).push({ open: '08:00', close: '18:00' })
}

function removeRange(weekday: number, index: number) {
  rangesFor(weekday).splice(index, 1)
}

async function save() {
  saving.value = true
  saved.value = false
  error.value = null

  try {
    // Days with no ranges are dropped rather than stored as empty arrays: absent
    // and "closed all day" mean the same thing, and one representation is enough.
    const payload = clone(form)

    payload.openingHours = Object.fromEntries(
      Object.entries(payload.openingHours).filter(([, ranges]) => ranges.length > 0),
    )

    await settings.save(payload)
    saved.value = true
  } catch (err) {
    error.value = messageFor(err)
  } finally {
    saving.value = false
  }
}

/** Surfaces the API's own validation message, which names the offending field. */
function messageFor(err: unknown): string {
  const response = (err as { response?: { data?: unknown } }).response

  if (typeof response?.data === 'string' && response.data.length > 0) {
    return response.data
  }

  return t('admin.saveFailed')
}

function clone(value: StoreSettings): StoreSettings {
  return JSON.parse(JSON.stringify(value)) as StoreSettings
}
</script>
