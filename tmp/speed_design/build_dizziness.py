from pathlib import Path

base = Path(__file__).with_name('build.py').read_text(encoding='utf-8')
exec(base.split("p('Speed System Design', 'Title')")[0])

p('Dizziness System Design', 'Title')
p('English Version', 'Subtitle')
p('Sustained high speed in ball form builds a hidden dizziness value. Players can switch to robot form to recover. A full value starts a warning grace period; failing to switch before it expires triggers a temporary dizziness state with no player control.')
h('1 Dizziness Value')
p('Dizziness value is an accumulated resource. Dizziness state is the temporary loss of control triggered after the grace period. Keep these terms distinct in implementation and UI.')
p('Only ball form accumulates dizziness, and only at or above the configurable high-speed threshold. Initially set this threshold equal to the small-enemy instant-kill threshold. Faster movement increases the accumulation rate. Expose the relationship between speed and accumulation rate for tuning.')
p('Before the grace period starts, ball-form speed below this threshold slowly reduces dizziness. Robot form rapidly reduces dizziness regardless of its current speed, including while it is still slowing down. Players can freely switch forms while under normal control. Clamp the value between zero and its configured maximum.')
h('2 Full Value and Grace Period')
p('Reaching the maximum value starts a configurable grace period, initially 2 to 3 seconds. The player retains control and receives a clear warning to switch to robot form.')
p('Once this grace period begins, slowing down no longer prevents dizziness. Only switching to robot form before the timer expires cancels the pending dizziness state. A lower speed or a falling value must not cancel, restart, or weaken the warning. Robot form cancels the warning and rapidly reduces the value. Switching back to ball form remains available; it uses the remaining value.')
h('3 Dizziness State')
p('If the grace period expires without a switch to robot form, force robot form, immediately set speed to zero, and disable all player actions, including movement, steering, acceleration, form switching, and skills. This immediate stop takes priority over normal robot-form deceleration.')
p('The loss of control lasts for a separately configurable duration, initially 2 to 3 seconds. Robot-form recovery reduces the dizziness value during this state. When the duration expires, restore control in robot form. The grace period and loss-of-control duration are independent parameters.')

doc.add_page_break()
h('4 Speed Bar Feedback')
p('Do not show a separate dizziness bar or exact numeric value. Convey increasing dizziness through stronger effects around the existing speed bar: edge ripples, offset ghost images, and a progressively faster pulse. Do not encode speed or dizziness with a color gradient or color changes.')
p('Keep the speed bar’s actual fill, frame of reference, labels, and both instant-kill threshold lines stable and readable. The threshold lines must not move or shake with the effects. Animate only the surrounding effects or ghost layers.')
table(['Stage','Feedback'],[
('Low value','Little or no surrounding effect.'),
('Medium value','Subtle edge ripples or ghosting with a slow pulse.'),
('High value','Stronger ghosting and faster pulses.'),
('Grace period','Persistent warning icon or short switch-to-robot prompt, with a distinct warning rhythm and a brief audio cue. Slowing down does not soften it.'),
('Dizziness state','Speed bar immediately reads zero. Show a dizziness icon and a recovery animation indicating the loss-of-control duration.'),
('Robot recovery','Outside the dizziness state, effects fade with the falling value. Returning to ball form uses the remaining value, not an automatic reset.')],[1.5,5.4])
h('5 Configurable Parameters')
p('Expose the maximum dizziness value; high-speed threshold; speed-to-accumulation-rate relationship; slow ball-form recovery rate; fast robot-form recovery rate; grace duration; loss-of-control duration; and effect-stage thresholds and intensity. The high-speed threshold initially matches the small-enemy instant-kill line. Tune each duration independently, initially within 2 to 3 seconds.')
h('6 Relationship to the Speed System')
p('Normal robot-form movement uses the constant deceleration defined in Speed System Design. Triggered dizziness overrides that rule by stopping the player immediately. The speed system owns movement, collision thresholds, and the speed bar scale; this document owns dizziness accumulation, recovery, warning timing, control loss, and associated effects.')

doc.add_page_break()
p('眩晕系统设计', 'Title')
p('中文版本', 'Subtitle')
p('玩家在球形态持续高速移动会积累隐藏的眩晕值，可主动切换为机器人形态恢复。眩晕值满后进入预警宽限期；到期前未切换为机器人，则触发暂时完全失去控制的眩晕状态。')
h('1 眩晕值')
p('眩晕值是积累资源，眩晕状态是宽限期结束后触发的短暂失控状态。实现和 UI 中应区分这两个概念。')
p('仅球形态积累眩晕值，且仅在速度达到或超过可配置的高速阈值时积累。高速阈值初始设为小型敌人秒杀阈值。速度越快，眩晕值增加越快；速度与积累速率之间的关系应向策划开放调试。')
p('宽限期开始前，球形态速度低于高速阈值时，眩晕值缓慢降低。机器人形态下眩晕值快速降低，与当前速度无关，在仍然减速的过程中也生效。正常可控时，玩家可以随时切换形态。眩晕值限制在零与配置的上限之间。')
h('2 满值与宽限期')
p('眩晕值达到上限时，开始可配置的宽限期，初步为 2 至 3 秒。此时玩家仍可操作，并收到明确的切换机器人预警。')
p('宽限期一旦开始，降低速度不再能避免眩晕。只有在倒计时结束前切换为机器人形态，才能取消本次待触发的眩晕状态。速度下降或眩晕值回落均不得取消、重启或减弱预警。进入机器人形态后取消预警，并快速降低眩晕值；仍可随时切回球形态，按剩余眩晕值继续运行。')
h('3 眩晕状态')
p('宽限期结束仍未切换为机器人形态时，强制进入机器人形态，速度立即降为零，并完全禁止玩家操作，包括移动、转向、加速、形态切换和技能。立即停速优先于常规的机器人形态减速。')
p('失控时长单独配置，初步为 2 至 3 秒。眩晕状态期间，眩晕值按机器人形态恢复规则降低；持续时间结束后恢复操作，玩家仍处于机器人形态。宽限期与眩晕失控时长是两个独立参数。')

doc.add_page_break()
h('4 速度条反馈')
p('不单独显示眩晕条或精确数值。通过现有速度条周围逐渐增强的特效表现眩晕程度，包括边缘波纹、偏移重影和逐渐加快的脉冲。不通过颜色渐变或颜色变化表达速度或眩晕程度。')
p('速度条的实际填充、位置基准、标签及两条秒杀刻度线保持稳定、清晰。刻度线不得随特效移动或晃动，仅周围特效或重影图层参与动画。')
table(['阶段','反馈'],[
('低眩晕值','基本没有外围特效。'),
('中眩晕值','轻微边缘波纹或重影，配合缓慢脉冲。'),
('高眩晕值','更明显的重影和更快的脉冲。'),
('宽限期','持续显示警告图标或切换机器人短提示，采用明确的警告节奏并配合短促提示音；不因降速而减弱。'),
('眩晕状态','速度条立即归零，显示眩晕图标及反映失控持续时间的恢复动画。'),
('机器人恢复','眩晕状态以外，特效随眩晕值下降而减弱；切回球形态后依据剩余值显示，不自动清零。')],[1.5,5.4])
h('5 可配置参数')
p('向策划开放眩晕值上限、高速阈值、速度与积累速率的关系、球形态低速缓慢恢复速率、机器人形态快速恢复速率、宽限期、眩晕失控时长，以及特效分段阈值与强度。高速阈值初始与小型敌人秒杀线一致；两种时长分别配置，初步均为 2 至 3 秒。')
h('6 与速度系统的关系')
p('常规机器人形态移动沿用速度系统设计中的固定减速度；触发眩晕时，以立即停速覆盖常规减速。速度系统文档负责移动、碰撞秒杀阈值和速度条刻度；本文档负责眩晕积累、恢复、预警计时、操作剥夺及关联特效。')

doc.core_properties.title = 'Dizziness System Design 眩晕系统设计'
doc.core_properties.author = ''
for st in doc.styles:
    for el in list(st.element.iter(qn('w:pBdr'))):
        el.getparent().remove(el)
out = ROOT / 'output' / 'docs' / 'Dizziness_System_Design_EN_ZH.docx'
doc.save(out)
print(out)
