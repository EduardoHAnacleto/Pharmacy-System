<template>
  <a
    href="https://wa.me/641111111111?text=Olá,%20gostaria%20de%20mais%20informações"
    target="_blank"
    class="whatsapp-float"
  >
    <i class="bi bi-whatsapp"></i>
  </a>
</template>

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
