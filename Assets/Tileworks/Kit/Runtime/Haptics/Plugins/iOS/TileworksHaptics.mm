#import <UIKit/UIKit.h>

// The Taptic Engine's feedback generators, created once. Kinds match Tileworks.Kit.Haptics.HapticKind.
static UISelectionFeedbackGenerator* selection;
static UIImpactFeedbackGenerator* light;
static UIImpactFeedbackGenerator* medium;
static UINotificationFeedbackGenerator* notification;

extern "C" void TileworksHapticsPlay(int kind)
{
    if (selection == nil)
    {
        selection = [[UISelectionFeedbackGenerator alloc] init];
        light = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight];
        medium = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleMedium];
        notification = [[UINotificationFeedbackGenerator alloc] init];
    }

    switch (kind)
    {
        case 0: [selection selectionChanged]; break;
        case 1: [light impactOccurred]; break;
        case 2: [medium impactOccurred]; break;
        case 3: [notification notificationOccurred:UINotificationFeedbackTypeSuccess]; break;
        case 4: [notification notificationOccurred:UINotificationFeedbackTypeWarning]; break;
        default: [notification notificationOccurred:UINotificationFeedbackTypeError]; break;
    }
}
