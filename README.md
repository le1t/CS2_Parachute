# CS2_Parachute
Плагин позволяет игрокам использовать парашют, удерживая кнопку USE (E) во время падения. При активации скорость падения значительно снижается, что даёт возможность мягко приземляться.

# https://www.youtube.com/watch?v=zQ39v9svDm0

# Требования
```
CounterStrikeSharp API версии 362 или выше
.NET 8.0 Runtime
```

# Конфигурационные параметры
```
css_parachute_enabled <0/1>, def.=1 – Включение/выключение плагина.
css_parachute_decrease_vector <0-500>, def.=50.0 – Величина уменьшения вертикальной скорости при активном парашюте (рекомендуемый диапазон 30–100).
css_parachute_linear_decrease <0/1>, def.=1 – Режим уменьшения скорости: 1 – линейное (постоянная скорость падения), 0 – аддитивное (постепенное замедление).
css_parachute_fall_speed <10-500>, def.=100.0 – Максимальная скорость падения при активном парашюте.
css_parachute_teleport_ticks <10-1000>, def.=300 – Количество тиков между принудительными обновлениями позиции игрока (для предотвращения ошибок предсказания).
css_parachute_log_level <0-5>, def.=4 – Уровень логирования (0-Trace,1-Debug,2-Info,3-Warning,4-Error,5-Critical).
```

# Консольные команды
```
css_parachute_help – Показать справку по плагину.
css_parachute_settings – Показать текущие настройки и статистику.
css_parachute_test – Вывести в чат инструкцию по использованию парашюта и текущие параметры.
css_parachute_reload – Перезагрузить конфигурацию и деактивировать все активные парашюты.
css_parachute_setenabled <0/1> – Установить значение css_parachute_enabled.
css_parachute_setdecreasevector <0-500> – Установить css_parachute_decrease_vector.
css_parachute_setlinear <0/1> – Установить css_parachute_linear_decrease.
css_parachute_setfallspeed <10-500> – Установить css_parachute_fall_speed.
css_parachute_setteleportticks <10-1000> – Установить css_parachute_teleport_ticks.
css_parachute_setloglevel <0-5> – Установить уровень логирования.
```

# ЭТОТ ПЛАГИН ФОРК ЭТОГО ПЛАГИНА https://github.com/Franc1sco/CS2-Parachute
