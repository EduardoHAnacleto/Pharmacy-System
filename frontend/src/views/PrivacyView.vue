<template>
  <section class="py-5">
    <div class="container px-4 px-lg-5">
      <div class="row justify-content-center">
        <div class="col-lg-8">
          <h2 class="fw-bolder mb-4">{{ t('privacy.title') }}</h2>

          <p class="lead">{{ t('privacy.intro') }}</p>

          <h5 class="mt-4">{{ t('privacy.browserTitle') }}</h5>
          <p>{{ t('privacy.browserBody') }}</p>

          <h5 class="mt-4">{{ t('privacy.serverTitle') }}</h5>
          <p>{{ t('privacy.serverBody') }}</p>

          <h5 class="mt-4">{{ t('privacy.analyticsTitle') }}</h5>
          <p>{{ t('privacy.analyticsBody') }}</p>

          <h5 class="mt-4">{{ t('privacy.cartTitle') }}</h5>
          <p>{{ t('privacy.cartBody') }}</p>

          <h5 class="mt-4">{{ t('privacy.contactTitle') }}</h5>
          <p>{{ t('privacy.contactBody') }}</p>

          <RouterLink to="/contact" class="btn btn-outline-primary">
            {{ t('contact.title') }}
          </RouterLink>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { RouterLink } from 'vue-router'
import { useI18n } from 'vue-i18n'

// Written as a page rather than a paragraph in the README because the claim it
// makes — that no personal data reaches the server — is the product's promise to
// the shop's customers, and it is enforced by a test against the mapped schema.
const { t } = useI18n()
</script>
